using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;

namespace CandC.HeicClipboard;

public enum UpdateCheckState
{
    UpToDate,
    UpdateAvailable,
    Failed
}

public sealed record UpdateCheckResult(UpdateCheckState State, UpdateManifest? Manifest, string? Error)
{
    public static UpdateCheckResult UpToDate() => new(UpdateCheckState.UpToDate, null, null);
    public static UpdateCheckResult Available(UpdateManifest manifest) => new(UpdateCheckState.UpdateAvailable, manifest, null);
    public static UpdateCheckResult Failed(string error) => new(UpdateCheckState.Failed, null, error);
}

public sealed class UpdateService
{
    private static readonly HttpClient Http = CreateHttpClient();
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DownloadStallTimeout = TimeSpan.FromSeconds(60);

    private readonly Uri _feedUri = new(AppConstants.UpdateFeedUrl);
    private readonly Uri _baseUri = new(AppConstants.UpdateBaseUrl);

    public static Version CurrentVersion
    {
        get
        {
            var assembly = typeof(UpdateService).Assembly;
            var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrEmpty(informational))
            {
                var plusIndex = informational.IndexOf('+');
                var candidate = plusIndex >= 0 ? informational[..plusIndex] : informational;
                if (Version.TryParse(candidate, out var parsed))
                {
                    return parsed;
                }
            }

            return assembly.GetName().Version ?? new Version(0, 0, 0);
        }
    }

    // Self-update swaps the single published exe in place. A development layout
    // (framework build with HeicToClipboard.dll next to the host exe) must not be swapped.
    public static bool IsSelfUpdateSupported =>
        Environment.ProcessPath is { } processPath &&
        string.Equals(Path.GetFileName(processPath), "HeicToClipboard.exe", StringComparison.OrdinalIgnoreCase) &&
        !File.Exists(Path.Combine(AppContext.BaseDirectory, "HeicToClipboard.dll"));

    public static void CleanupStaleBackup()
    {
        try
        {
            if (Environment.ProcessPath is { } processPath)
            {
                var backupPath = processPath + ".old";
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
            }
        }
        catch
        {
            // The previous exe may still be shutting down; the next launch retries.
        }
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        string json;
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(CheckTimeout);
            json = await Http.GetStringAsync(_feedUri, timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return UpdateCheckResult.Failed($"Update check failed: {exception.Message}");
        }

        if (!UpdateManifest.TryParse(json, out var manifest, out var error))
        {
            return UpdateCheckResult.Failed(error);
        }

        return manifest!.IsNewerThan(CurrentVersion)
            ? UpdateCheckResult.Available(manifest)
            : UpdateCheckResult.UpToDate();
    }

    public async Task<string> DownloadAsync(UpdateManifest manifest, IProgress<int>? percentProgress, CancellationToken cancellationToken = default)
    {
        var downloadPath = Path.Combine(Path.GetTempPath(), $"{AppConstants.TempFilePrefix}update_{Guid.NewGuid():N}.exe");

        try
        {
            // The stall timer is re-armed on every received chunk, so slow lines are fine
            // but a server that stops sending data aborts the download instead of hanging.
            using var stallSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            stallSource.CancelAfter(DownloadStallTimeout);

            try
            {
                using var response = await Http.GetAsync(
                    manifest.ResolveDownloadUri(_baseUri),
                    HttpCompletionOption.ResponseHeadersRead,
                    stallSource.Token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                await using (var source = await response.Content.ReadAsStreamAsync(stallSource.Token).ConfigureAwait(false))
                await using (var target = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[81920];
                    long totalRead = 0;
                    while (true)
                    {
                        stallSource.CancelAfter(DownloadStallTimeout);
                        var read = await source.ReadAsync(buffer, stallSource.Token).ConfigureAwait(false);
                        if (read == 0)
                        {
                            break;
                        }

                        await target.WriteAsync(buffer.AsMemory(0, read), stallSource.Token).ConfigureAwait(false);
                        totalRead += read;
                        percentProgress?.Report((int)Math.Clamp(totalRead * 100 / manifest.Size, 0, 100));
                    }
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException("The download timed out because no data arrived for a while.");
            }

            VerifyDownload(downloadPath, manifest);
            return downloadPath;
        }
        catch
        {
            TryDelete(downloadPath);
            throw;
        }
    }

    public void ApplyAndRestart(string downloadedFilePath)
    {
        if (!IsSelfUpdateSupported || Environment.ProcessPath is not { } exePath)
        {
            throw new InvalidOperationException("Self-update is only supported for the installed HeicToClipboard.exe.");
        }

        var backupPath = exePath + ".old";
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }

        // Windows allows renaming a running exe, so the swap works without a helper process.
        File.Move(exePath, backupPath);
        try
        {
            File.Move(downloadedFilePath, exePath);
        }
        catch
        {
            File.Move(backupPath, exePath);
            throw;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true
            });
        }
        catch
        {
            // Restore the previous exe so context-menu conversions keep working.
            try
            {
                // Move the failed replacement aside so it can be inspected; best effort.
                File.Move(exePath, downloadedFilePath);
            }
            catch
            {
                // Ignore; the overwriting restore below handles a leftover replacement.
            }

            try
            {
                File.Move(backupPath, exePath, overwrite: true);
            }
            catch
            {
                // Keep the original startup error; it is more useful than a failed rollback.
            }

            throw;
        }
    }

    internal static void VerifyDownload(string filePath, UpdateManifest manifest)
    {
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length != manifest.Size)
        {
            throw new InvalidOperationException(
                $"The downloaded update has the wrong size ({fileInfo.Length} bytes, expected {manifest.Size}).");
        }

        using var stream = File.OpenRead(filePath);
        var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (!string.Equals(hash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The downloaded update failed the checksum verification.");
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            // Downloads can take a while on slow lines; operations pass their own timeout or token.
            Timeout = Timeout.InfiniteTimeSpan
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"HeicToClipboard/{CurrentVersion}");
        return client;
    }

    private static void TryDelete(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Best effort cleanup of a temp file.
        }
    }
}
