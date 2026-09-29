namespace CandC.HeicClipboard.Tests;

public sealed class OutputPathResolverTests : IDisposable
{
    private readonly string _workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public void Resolve_UsesTempFolderWhenCustomOutputIsDisabled()
    {
        var tempPath = Path.Combine(_workingDirectory, "temp");
        var settings = HeicToClipboardSettings.CreateDefault();

        var resolved = OutputPathResolver.Resolve(settings, tempPath);

        Assert.Equal(tempPath, resolved.WorkingDirectory, ignoreCase: true);
        Assert.True(resolved.CleanupEnabled);
        Assert.Equal(TimeSpan.FromDays(1), resolved.CleanupAge);
    }

    [Fact]
    public void Resolve_UsesCustomFolderWhenEnabled()
    {
        var tempPath = Path.Combine(_workingDirectory, "temp");
        var customPath = Path.Combine(_workingDirectory, "custom");
        var settings = new HeicToClipboardSettings
        {
            UseCustomOutputFolder = true,
            CustomOutputFolder = customPath
        };

        var resolved = OutputPathResolver.Resolve(settings, tempPath);

        Assert.Equal(customPath, resolved.WorkingDirectory, ignoreCase: true);
        Assert.False(resolved.CleanupEnabled);
        Assert.True(Directory.Exists(customPath));
    }

    [Fact]
    public void Resolve_UnavailableCustomFolder_FailsWithoutCreatingTempFallback()
    {
        Directory.CreateDirectory(_workingDirectory);
        var blockedPath = Path.Combine(_workingDirectory, "blocked");
        File.WriteAllText(blockedPath, "unrelated file");
        var tempPath = Path.Combine(_workingDirectory, "temp");
        var settings = new HeicToClipboardSettings { UseCustomOutputFolder = true, CustomOutputFolder = blockedPath };

        var error = Assert.Throws<IOException>(() => OutputPathResolver.Resolve(settings, tempPath));

        Assert.Contains(blockedPath, error.Message);
        Assert.Contains("No files were converted", error.Message);
        Assert.False(Directory.Exists(tempPath));
        Assert.Equal("unrelated file", File.ReadAllText(blockedPath));
    }

    [Fact]
    public void Resolve_EmptyCustomFolder_FailsWithoutCreatingTempFallback()
    {
        var tempPath = Path.Combine(_workingDirectory, "temp");
        var settings = new HeicToClipboardSettings { UseCustomOutputFolder = true, CustomOutputFolder = " " };

        Assert.Throws<IOException>(() => OutputPathResolver.Resolve(settings, tempPath));
        Assert.False(Directory.Exists(tempPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
    }
}
