namespace CandC.HeicClipboard.Tests;

public sealed class TempFileManagerTests : IDisposable
{
    private readonly string _workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public void GetExpiredFiles_ReturnsOnlyOldManagedFiles()
    {
        Directory.CreateDirectory(_workingDirectory);
        var manager = new TempFileManager(_workingDirectory);
        var now = DateTime.UtcNow;

        var expiredManagedFile = Path.Combine(_workingDirectory, $"{AppConstants.TempFilePrefix}expired.jpg");
        var freshManagedFile = Path.Combine(_workingDirectory, $"{AppConstants.TempFilePrefix}fresh.jpg");
        var unrelatedFile = Path.Combine(_workingDirectory, "other.jpg");

        File.WriteAllText(expiredManagedFile, "old");
        File.WriteAllText(freshManagedFile, "fresh");
        File.WriteAllText(unrelatedFile, "other");

        File.SetLastWriteTimeUtc(expiredManagedFile, now - AppConstants.TempFileMaxAge - TimeSpan.FromMinutes(5));
        File.SetLastWriteTimeUtc(freshManagedFile, now - TimeSpan.FromHours(1));
        File.SetLastWriteTimeUtc(unrelatedFile, now - AppConstants.TempFileMaxAge - TimeSpan.FromDays(1));

        var expiredFiles = manager.GetExpiredFiles(now);

        Assert.Single(expiredFiles);
        Assert.Equal(expiredManagedFile, expiredFiles[0], ignoreCase: true);
    }

    [Fact]
    public void GetExpiredFiles_ReturnsEmptyWhenCleanupIsDisabled()
    {
        Directory.CreateDirectory(_workingDirectory);
        var manager = new TempFileManager(_workingDirectory, cleanupEnabled: false);
        var expiredManagedFile = Path.Combine(_workingDirectory, $"{AppConstants.TempFilePrefix}expired.jpg");
        File.WriteAllText(expiredManagedFile, "old");
        File.SetLastWriteTimeUtc(expiredManagedFile, DateTime.UtcNow - TimeSpan.FromDays(10));

        var expiredFiles = manager.GetExpiredFiles(DateTime.UtcNow);

        Assert.Empty(expiredFiles);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
    }
}
