namespace CandC.HeicClipboard.Tests;

public sealed class ImageResizePlannerTests
{
    [Fact]
    public void FitWithin_ReturnsOriginalSizeWhenNoMaxDimensionIsSet()
    {
        var fitted = ImageResizePlanner.FitWithinLongestSide(4032, 3024, null);

        Assert.Equal(new ImageDimensions(4032, 3024), fitted);
    }

    [Fact]
    public void FitWithinLongestSide_ScalesLandscapeWhilePreservingAspectRatio()
    {
        var fitted = ImageResizePlanner.FitWithinLongestSide(4032, 3024, 2048);

        Assert.Equal(2048, fitted.Width);
        Assert.Equal(1536, fitted.Height);
    }

    [Fact]
    public void FitWithinLongestSide_ScalesPortraitWhilePreservingAspectRatio()
    {
        var fitted = ImageResizePlanner.FitWithinLongestSide(3024, 4032, 2048);

        Assert.Equal(1536, fitted.Width);
        Assert.Equal(2048, fitted.Height);
    }
}
