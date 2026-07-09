namespace CandC.HeicClipboard.Tests;

public sealed class HeicToClipboardSettingsTests
{
    [Theory]
    [InlineData(1000, 500)]
    [InlineData(501, 500)]
    public void Sanitize_ClampsMaxFileSizeAboveUpperBound(decimal input, decimal expected)
    {
        var settings = new HeicToClipboardSettings { MaxFileSizeMb = input };

        var sanitized = settings.Sanitize();

        Assert.Equal(expected, sanitized.MaxFileSizeMb);
    }

    [Fact]
    public void Sanitize_ClampsMaxFileSizeBelowLowerBound()
    {
        var settings = new HeicToClipboardSettings { MaxFileSizeMb = 0.05m };

        var sanitized = settings.Sanitize();

        Assert.Equal(AppConstants.MinimumFileSizeMb, sanitized.MaxFileSizeMb);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Sanitize_UsesDefaultMaxFileSizeWhenNotPositive(decimal input)
    {
        var settings = new HeicToClipboardSettings { MaxFileSizeMb = input };

        var sanitized = settings.Sanitize();

        Assert.Equal(AppConstants.DefaultMaximumFileSizeMb, sanitized.MaxFileSizeMb);
    }

    [Theory]
    [InlineData(10000, 3650)]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(30, 30)]
    public void Sanitize_ClampsTempCleanupDays(int input, int expected)
    {
        var settings = new HeicToClipboardSettings { TempCleanupDays = input };

        var sanitized = settings.Sanitize();

        Assert.Equal(expected, sanitized.TempCleanupDays);
    }

    [Fact]
    public void Sanitize_KeepsValuesWithinBoundsUnchanged()
    {
        var settings = new HeicToClipboardSettings
        {
            MaxFileSizeMb = 7.5m,
            TempCleanupDays = 14
        };

        var sanitized = settings.Sanitize();

        Assert.Equal(7.5m, sanitized.MaxFileSizeMb);
        Assert.Equal(14, sanitized.TempCleanupDays);
    }
}
