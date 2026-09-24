using System.Buffers;
using System.Buffers.Text;
using Rove.Core.Protocol;

namespace Rove.Core.Services;

public sealed partial class LinuxAdminSession
{
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
}
