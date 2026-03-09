namespace CandC.HeicClipboard.Tests;

public sealed class HeicToClipboardSettingsStoreTests : IDisposable
{
    private readonly string _workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public void Load_ReturnsDefaultsWhenFileIsMissing()
    {
        var store = new HeicToClipboardSettingsStore(Path.Combine(_workingDirectory, AppConstants.SettingsFileName));

        var settings = store.Load();

        Assert.False(settings.UseCustomOutputFolder);
        Assert.Equal(AppConstants.DefaultMaximumFileSizeMb, settings.MaxFileSizeMb);
        Assert.Equal(AppConstants.DefaultInitialJpegQuality, settings.InitialJpegQuality);
        Assert.True(settings.KeepOriginalResolution);
        Assert.Null(settings.MaxLongestSidePx);
        Assert.Equal(AppConstants.DefaultTempCleanupDays, settings.TempCleanupDays);
    }

    [Fact]
    public void Load_ReturnsDefaultsWhenJsonIsInvalid()
    {
        Directory.CreateDirectory(_workingDirectory);
        var settingsPath = Path.Combine(_workingDirectory, AppConstants.SettingsFileName);
        File.WriteAllText(settingsPath, "{ not valid json");
        var store = new HeicToClipboardSettingsStore(settingsPath);

        var settings = store.Load();

        Assert.Equal(AppConstants.DefaultMaximumFileSizeMb, settings.MaxFileSizeMb);
        Assert.Equal(AppConstants.DefaultInitialJpegQuality, settings.InitialJpegQuality);
    }

    [Fact]
    public void Save_AndLoad_RoundTripAllFields()
    {
        var settingsPath = Path.Combine(_workingDirectory, AppConstants.SettingsFileName);
        var store = new HeicToClipboardSettingsStore(settingsPath);
        var settings = new HeicToClipboardSettings
        {
            UseCustomOutputFolder = true,
            CustomOutputFolder = Path.Combine(_workingDirectory, "output"),
            MaxFileSizeMb = 7.5m,
            InitialJpegQuality = 88,
            KeepOriginalResolution = false,
            MaxLongestSidePx = 2560,
            TempCleanupDays = 3
        };

        store.Save(settings);
        var reloaded = store.Load();

        Assert.True(reloaded.UseCustomOutputFolder);
        Assert.Equal(settings.CustomOutputFolder, reloaded.CustomOutputFolder);
        Assert.Equal(7.5m, reloaded.MaxFileSizeMb);
        Assert.Equal(88, reloaded.InitialJpegQuality);
        Assert.False(reloaded.KeepOriginalResolution);
        Assert.Equal(2560, reloaded.MaxLongestSidePx);
        Assert.Equal(3, reloaded.TempCleanupDays);
    }

    [Fact]
    public void Load_MapsLegacyMaxDimensionToLongestSide()
    {
        Directory.CreateDirectory(_workingDirectory);
        var settingsPath = Path.Combine(_workingDirectory, AppConstants.SettingsFileName);
        File.WriteAllText(settingsPath, """
        {
          "maxDimensionPx": 2048
        }
        """);
        var store = new HeicToClipboardSettingsStore(settingsPath);

        var settings = store.Load();

        Assert.False(settings.KeepOriginalResolution);
        Assert.Equal(2048, settings.MaxLongestSidePx);
    }

    [Fact]
    public void Load_MapsLegacyWidthAndHeightToLongestSide()
    {
        Directory.CreateDirectory(_workingDirectory);
        var settingsPath = Path.Combine(_workingDirectory, AppConstants.SettingsFileName);
        File.WriteAllText(settingsPath, """
        {
          "maxWidthPx": 2560,
          "maxHeightPx": 1440
        }
        """);
        var store = new HeicToClipboardSettingsStore(settingsPath);

        var settings = store.Load();

        Assert.False(settings.KeepOriginalResolution);
        Assert.Equal(2560, settings.MaxLongestSidePx);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
    }
}
