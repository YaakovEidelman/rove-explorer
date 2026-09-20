using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Rove.Core.Protocol;

namespace Rove.Core.Services;

[SupportedOSPlatform("linux")]
public sealed class LinuxAdminSession : IAdminSession
{
    private const string PkexecPath = "/usr/bin/pkexec";
    private const string EndMarker = "END";
    private const int FieldsPerEntry = 6;
    private const int PkexecDismissed = 126;
    private const int PkexecRefused = 127;

    internal const string Script =
        "export PATH=/usr/bin:/bin LC_ALL=C; " +
        "IFS= read -r -d '' secret || exit 1; " +
        "while IFS= read -r -d '' token && IFS= read -r -d '' verb " +
        "&& IFS= read -r -d '' path && IFS= read -r -d '' limit; do " +
        "[ \"$token\" = \"$secret\" ] || exit 1; " +
        "case \"$path\" in /*) ;; *) exit 1;; esac; " +
        "case \"$verb\" in " +
        "list) " +
        "find -H \"$path\" -mindepth 1 -maxdepth 1 " +
        "-printf '%y\\0%Y\\0%s\\0%T@\\0%f\\0%l\\0' 2>/dev/null; " +
        "printf 'END\\0%s\\0' \"$?\";; " +
        "read) " +
        "case \"$limit\" in ''|*[!0-9]*) exit 1;; esac; " +
        "if [ -f \"$path\" ]; then " +
        "printf 'OK\\0'; head -c \"$limit\" -- \"$path\" 2>/dev/null | base64 -w0; printf '\\0'; " +
        "else printf 'FAIL\\0'; fi;; " +
        "*) exit 1;; " +
        "esac; " +
        "done";

    private readonly string _program;
    private readonly string[] _arguments;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly byte[] _buffer = new byte[16 * 1024];
    private readonly List<byte> _token = [];
    private readonly StringBuilder _errors = new();
    private readonly string _secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private Process? _process;
    private Stream? _output;
    private string? _copyRoot;
    private int _copyCount;
    private int _bufferPosition;
    private int _bufferLength;

    internal LinuxAdminSession(string program, string[] arguments)
    {
        _program = program;
        _arguments = arguments;
    }

    public static LinuxAdminSession? ForPkexec()
    {
        string? bash = FindBash();
        if (bash is null || !File.Exists(PkexecPath))
            return null;
        return new LinuxAdminSession(PkexecPath, [bash, "-c", Script]);
    }

    private static string? FindBash() =>
        new[] { "/usr/bin/bash", "/bin/bash" }.FirstOrDefault(File.Exists);

    public bool IsRunning => _process is { HasExited: false };

    public async Task<CommandResult<FolderItem[]>> ReadDirectoryAsync(string path)
    {
        if (!path.StartsWith('/') || path.Contains('\0'))
            return CommandResult<FolderItem[]>.Fail("bad_path", $"Not an absolute path: {path}");

        await _gate.WaitAsync();
        try
        {
            if (_process is null)
                Start();
            await SendAsync("list", path, string.Empty);
            return await ReadListingAsync(path);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException
            or System.ComponentModel.Win32Exception)
        {
            return CommandResult<FolderItem[]>.Fail("admin_failed", await StopAsync(ex.Message));
        }
        finally
        {
            _gate.Release();
        }
    }

    private void Start()
    {
        ProcessStartInfo start = new(_program)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in _arguments)
            start.ArgumentList.Add(argument);

        _errors.Clear();
        _bufferPosition = 0;
        _bufferLength = 0;
        Process process = new() { StartInfo = start };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null && _errors.Length < 2048)
                _errors.AppendLine(e.Data);
        };
        process.Start();
        process.BeginErrorReadLine();
        _output = process.StandardOutput.BaseStream;
        _process = process;

        Stream input = process.StandardInput.BaseStream;
        input.Write([.. Encoding.ASCII.GetBytes(_secret), 0]);
        input.Flush();
    }

    private async Task SendAsync(string verb, string path, string limit)
    {
        Stream input = _process!.StandardInput.BaseStream;
        List<byte> bytes = [];
        foreach (string field in new[] { _secret, verb, path, limit })
        {
            bytes.AddRange(Encoding.UTF8.GetBytes(field));
            bytes.Add(0);
        }
        await input.WriteAsync(bytes.ToArray());
        await input.FlushAsync();
    }

    private async Task<CommandResult<FolderItem[]>> ReadListingAsync(string path)
    {
        List<FolderItem> items = [];
        while (true)
        {
            string? first = await ReadTokenAsync();
            if (first is null)
                return await StoppedAsync<FolderItem[]>();

            if (first == EndMarker)
            {
                string? status = await ReadTokenAsync();
                if (status is null)
                    return await StoppedAsync<FolderItem[]>();
                if (status != "0" && items.Count == 0)
                    return CommandResult<FolderItem[]>.Fail(
                        "permission_denied", $"Access denied even as administrator: {path}");
                return CommandResult<FolderItem[]>.Ok([.. items]);
            }

            string[] fields = new string[FieldsPerEntry];
            fields[0] = first;
            for (int i = 1; i < FieldsPerEntry; i++)
            {
                string? field = await ReadTokenAsync();
                if (field is null)
                    return await StoppedAsync<FolderItem[]>();
                fields[i] = field;
            }
            items.Add(ToItem(path, fields));
        }
    }

    private static FolderItem ToItem(string directory, string[] fields)
    {
        string entryType = fields[0];
        bool isDirectory = fields[1] == "d";
        string name = fields[4];

        FileAttributes attributes = 0;
        if (isDirectory)
            attributes |= FileAttributes.Directory;
        if (entryType == "l")
            attributes |= FileAttributes.ReparsePoint;
        if (name.StartsWith('.'))
            attributes |= FileAttributes.Hidden;
        if (attributes == 0)
            attributes = FileAttributes.Normal;

        long size = long.TryParse(fields[2], out long parsed) ? parsed : 0;
        double seconds = double.TryParse(
            fields[3], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double stamp) ? stamp : 0;

        return new FolderItem(
            name,
            Path.Combine(directory, name),
            attributes,
            DateTimeOffset.FromUnixTimeSeconds((long)seconds).LocalDateTime,
            isDirectory,
            isDirectory ? null : size,
            isDirectory ? string.Empty : Path.GetExtension(name));
    }

    private async Task<string?> ReadTokenAsync()
    {
        _token.Clear();
        while (true)
        {
            if (_bufferPosition == _bufferLength)
            {
                _bufferLength = await _output!.ReadAsync(_buffer);
                _bufferPosition = 0;
                if (_bufferLength == 0)
                    return null;
            }

            int end = Array.IndexOf(_buffer, (byte)0, _bufferPosition, _bufferLength - _bufferPosition);
            if (end < 0)
            {
                _token.AddRange(_buffer.AsSpan(_bufferPosition, _bufferLength - _bufferPosition));
                _bufferPosition = _bufferLength;
                continue;
            }

            _token.AddRange(_buffer.AsSpan(_bufferPosition, end - _bufferPosition));
            _bufferPosition = end + 1;
            return Encoding.UTF8.GetString([.. _token]);
        }
    }

    private async Task<CommandResult<T>> StoppedAsync<T>() =>
        CommandResult<T>.Fail("admin_failed", await StopAsync(null));

    private async Task<string> StopAsync(string? detail)
    {
        Process? process = _process;
        _process = null;
        int? exitCode = null;
        if (process is not null)
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
                exitCode = process.ExitCode;
            }
            catch (OperationCanceledException)
            {
            }
            process.Dispose();
        }

        return exitCode switch
        {
            PkexecDismissed => "Administrator access was cancelled.",
            PkexecRefused => "Administrator access was refused.",
            _ => detail ?? (_errors.Length > 0 ? _errors.ToString().Trim() : "The administrator helper stopped."),
        };
    }

    public async Task<CommandResult<string>> CopyToTempAsync(
        string path, long maxBytes, CancellationToken ct = default)
    {
        if (!path.StartsWith('/') || path.Contains('\0'))
            return CommandResult<string>.Fail("bad_path", $"Not an absolute path: {path}");

        await _gate.WaitAsync(ct);
        string? folder = null;
        try
        {
            if (_process is null)
                Start();
            await SendAsync("read", path, Math.Max(0, maxBytes).ToString());

            string? status = await ReadTokenAsync();
            if (status is null)
                return await StoppedAsync<string>();
            if (status != "OK")
                return CommandResult<string>.Fail("not_a_file", $"Could not read {path}");

            folder = NewCopyFolder();
            string copy = Path.Combine(folder, CopyName(path));
            await using (FileStream file = new(copy, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
            }))
            {
                if (!await ReadBase64TokenAsync(file))
                {
                    DeleteQuietly(folder);
                    return await StoppedAsync<string>();
                }
            }
            File.SetUnixFileMode(copy, UnixFileMode.UserRead);
            return CommandResult<string>.Ok(copy);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException
            or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            if (folder is not null)
                DeleteQuietly(folder);
            return CommandResult<string>.Fail("admin_failed", await StopAsync(ex.Message));
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Discard(string copy)
    {
        string? folder = Path.GetDirectoryName(copy);
        if (_copyRoot is not null && folder is not null
            && Path.GetDirectoryName(folder) == _copyRoot)
        {
            DeleteQuietly(folder);
        }
    }

    private string NewCopyFolder()
    {
        UnixFileMode mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        if (_copyRoot is null)
        {
            string runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") is { Length: > 0 } dir
                && Directory.Exists(dir) ? dir : Path.GetTempPath();
            string root = Path.Combine(runtime, "rove-admin-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root, mode);
            _copyRoot = root;
        }
        string folder = Path.Combine(_copyRoot, (++_copyCount).ToString());
        Directory.CreateDirectory(folder, mode);
        return folder;
    }

    private static string CopyName(string path)
    {
        string name = Path.GetFileName(path);
        return name is "" or "." or ".." ? "file" : name;
    }

    private static void DeleteQuietly(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private async Task<bool> ReadBase64TokenAsync(Stream destination)
    {
        byte[] carry = [];
        while (true)
        {
            if (_bufferPosition == _bufferLength)
            {
                _bufferLength = await _output!.ReadAsync(_buffer);
                _bufferPosition = 0;
                if (_bufferLength == 0)
                    return false;
            }

            int end = Array.IndexOf(_buffer, (byte)0, _bufferPosition, _bufferLength - _bufferPosition);
            int segmentEnd = end < 0 ? _bufferLength : end;
            byte[] input = [.. carry, .. _buffer.AsSpan(_bufferPosition, segmentEnd - _bufferPosition)];
            _bufferPosition = end < 0 ? _bufferLength : end + 1;

            bool final = end >= 0;
            int usable = final ? input.Length : Math.Max(0, (input.Length - 1) / 4 * 4);
            byte[] decoded = new byte[usable / 4 * 3 + 3];
            OperationStatus status = Base64.DecodeFromUtf8(
                input.AsSpan(0, usable), decoded, out _, out int written, isFinalBlock: final);
            if (status != OperationStatus.Done)
                throw new IOException("The administrator helper sent data that is not valid.");
            await destination.WriteAsync(decoded.AsMemory(0, written));
            carry = input[usable..];

            if (final)
                return true;
        }
    }

    public void Dispose()
    {
        if (_copyRoot is not null)
            DeleteQuietly(_copyRoot);
        Process? process = _process;
        _process = null;
        if (process is null)
            return;
        try
        {
            process.StandardInput.Close();
            process.WaitForExit(500);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
        }
        process.Dispose();
    }
}
