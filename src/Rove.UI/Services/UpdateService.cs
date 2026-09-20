using Rove.Core.Services;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Rove.UI.Services;

public sealed class UpdateService
{
    private const string Owner = "YaakovEidelman";
    private const string Repo = "rove-explorer";

    private readonly HttpClient _client;
    private bool _busy;

    public UpdateService(HttpClient client)
    {
        _client = client;
    }

    public event Action<string>? InfoRaised;
    public event Action<string>? ErrorRaised;

    public async Task CheckInBackgroundAsync(bool autoUpdate, CancellationToken ct)
    {
        UpdateCheckState? state = UpdateCheckState.Read(RovePaths.UpdateCheckStateFile);
        if (!UpdateChecker.DueForCheck(state, DateTime.UtcNow))
            return;

        GitHubRelease? release = await UpdateChecker.FetchLatestAsync(_client, Owner, Repo, ct).ConfigureAwait(false);
        new UpdateCheckState(DateTime.UtcNow, state?.SkippedVersion).Write(RovePaths.UpdateCheckStateFile);

        if (release is null || !UpdateChecker.IsOfferable(release, RunningVersion(), state?.SkippedVersion))
            return;

        if (autoUpdate)
            await DownloadAndInstallAsync(release, progress: null, ct).ConfigureAwait(false);
        else
            InfoRaised?.Invoke($"Rove {release.Version} is available — Ctrl+U to update.");
    }

    public async Task CheckNowAsync(CancellationToken ct)
    {
        InfoRaised?.Invoke("Checking for updates…");
        GitHubRelease? release = await UpdateChecker.FetchLatestAsync(_client, Owner, Repo, ct).ConfigureAwait(false);
        if (release is null)
        {
            ErrorRaised?.Invoke("Couldn't reach GitHub to check for updates.");
            return;
        }
        if (release.Version is not { } version || version <= RunningVersion())
        {
            InfoRaised?.Invoke("Rove is up to date.");
            return;
        }
        await DownloadAndInstallAsync(release, progress: null, ct).ConfigureAwait(false);
    }

    public async Task DownloadAndInstallAsync(GitHubRelease release, IProgress<double>? progress, CancellationToken ct)
    {
        if (_busy)
        {
            InfoRaised?.Invoke("An update is already in progress.");
            return;
        }
        _busy = true;
        try
        {
            string assetName = OperatingSystem.IsWindows() ? "rove-win-x64.zip" : "rove-linux-x64.tar.gz";
            if (release.AssetNamed(assetName) is not { } asset)
            {
                ErrorRaised?.Invoke("The latest release has no build for this platform.");
                return;
            }

            string staging = Path.Combine(RovePaths.StateDirectory, "updates", release.TagName.TrimStart('v'));
            Directory.CreateDirectory(staging);
            string archive = Path.Combine(staging, asset.Name);

            await DownloadAsync(asset.BrowserDownloadUrl, archive, progress, ct).ConfigureAwait(false);

            string extracted = Path.Combine(staging, "extracted");
            Extract(archive, extracted, assetName);

            string executableName = OperatingSystem.IsWindows() ? "Rove.exe" : "Rove";
            string executable = Path.Combine(extracted, executableName);
            if (!File.Exists(executable))
                executable = Path.Combine(extracted, "rove", executableName);
            if (!File.Exists(executable))
            {
                ErrorRaised?.Invoke("The downloaded build did not contain Rove.");
                return;
            }
            if (!OperatingSystem.IsWindows())
                MakeExecutable(executable);

            ProcessStartInfo startInfo = new(executable, "--install") { UseShellExecute = false };
            using Process? installer = Process.Start(startInfo);
            if (installer is not null)
                await installer.WaitForExitAsync(ct).ConfigureAwait(false);

            Directory.Delete(staging, recursive: true);
            InfoRaised?.Invoke("Update installed — restart Rove to use it.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException
            or InvalidDataException or Win32Exception or OperationCanceledException)
        {
            ErrorRaised?.Invoke("Couldn't install the update: " + ex.Message);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task DownloadAsync(string url, string destination, IProgress<double>? progress, CancellationToken ct)
    {
        using HttpResponseMessage response =
            await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        long? total = response.Content.Headers.ContentLength;

        using Stream source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using FileStream destinationStream = File.Create(destination);
        byte[] buffer = new byte[81920];
        long readSoFar = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await destinationStream.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            readSoFar += read;
            if (total is > 0)
                progress?.Report(readSoFar * 100.0 / total.Value);
        }
    }

    private static void Extract(string archive, string destination, string assetName)
    {
        Directory.CreateDirectory(destination);
        if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(archive, destination, overwriteFiles: true);
            return;
        }
        using FileStream fileStream = File.OpenRead(archive);
        using GZipStream gzip = new(fileStream, CompressionMode.Decompress);
        TarFile.ExtractToDirectory(gzip, destination, overwriteFiles: true);
    }

    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    private static void MakeExecutable(string path) =>
        File.SetUnixFileMode(path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

    private static Version RunningVersion() =>
        typeof(UpdateService).Assembly.GetName().Version ?? new Version(0, 0);
}
