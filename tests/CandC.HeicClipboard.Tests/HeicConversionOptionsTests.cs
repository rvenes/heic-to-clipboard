namespace CandC.HeicClipboard.Tests;

public sealed class HeicConversionOptionsTests
{
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
    public void SizeLimitExceededMessage_UsesInvariantDecimalSeparator()
    {
        var settings = new HeicToClipboardSettings { MaxFileSizeMb = 0.5m };

        var options = HeicConversionOptions.FromSettings(settings);

        Assert.Equal("Could not keep the JPEG under 0.5 MB.", options.SizeLimitExceededMessage);
    }
}
