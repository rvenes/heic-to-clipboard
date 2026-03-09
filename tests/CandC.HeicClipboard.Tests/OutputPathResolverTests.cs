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

    public void Dispose()
    {
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
    }
}
