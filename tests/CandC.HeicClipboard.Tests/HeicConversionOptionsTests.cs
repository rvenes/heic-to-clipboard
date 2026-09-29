namespace CandC.HeicClipboard.Tests;

public sealed class HeicConversionOptionsTests
{
    [Fact]
    public void DefaultLimit_FitsDecimalTenMegabyteUploadLimit()
    {
        var options = HeicConversionOptions.FromSettings(HeicToClipboardSettings.CreateDefault());

        Assert.Equal(9_800_000, options.MaximumBytes);
        Assert.True(options.MaximumBytes < 10_000_000);
        Assert.Equal(150_000, AppConstants.ToBytes(0.15m));
        Assert.Equal(1, AppConstants.ToBytes(0.0000019m));
    }

    [Fact]
    public void SizeLimitExceededMessage_UsesDefaultLimit()
    {
        var options = HeicConversionOptions.FromSettings(HeicToClipboardSettings.CreateDefault());

        Assert.Equal("Could not keep the JPEG under 9.8 MB.", options.SizeLimitExceededMessage);
    }

    [Fact]
    public void SizeLimitExceededMessage_UsesConfiguredLimit()
    {
        var settings = new HeicToClipboardSettings { MaxFileSizeMb = 2m };

        var options = HeicConversionOptions.FromSettings(settings);

        Assert.Equal("Could not keep the JPEG under 2 MB.", options.SizeLimitExceededMessage);
    }

    [Fact]
    public void SizeLimitExceededMessage_FallsBackToBytesForTinyLimits()
    {
        var options = new HeicConversionOptions(300, 95, true, null);

        Assert.Equal("Could not keep the JPEG under 300 bytes.", options.SizeLimitExceededMessage);
    }

    [Fact]
    public void SizeLimitExceededMessage_UsesInvariantDecimalSeparator()
    {
        var settings = new HeicToClipboardSettings { MaxFileSizeMb = 0.5m };

        var options = HeicConversionOptions.FromSettings(settings);

        Assert.Equal("Could not keep the JPEG under 0.5 MB.", options.SizeLimitExceededMessage);
    }
}
