using System.Diagnostics;
using System.Text;
using Rove.Core.Protocol;

namespace Rove.Core.Services;

public sealed partial class LinuxAdminSession
{
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
}
