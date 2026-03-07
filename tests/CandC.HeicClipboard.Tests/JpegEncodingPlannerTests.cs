namespace CandC.HeicClipboard.Tests;

public sealed class JpegEncodingPlannerTests
{
    [Fact]
    public void CreateAttempts_StartsWithOriginalScaleQualitySteps()
    {
        var attempts = JpegEncodingPlanner.CreateAttempts();

        Assert.Equal(new JpegEncodingAttempt(100, 95), attempts[0]);
        Assert.Equal(new JpegEncodingAttempt(100, 92), attempts[1]);
        Assert.Equal(new JpegEncodingAttempt(100, 90), attempts[2]);
    }

    [Fact]
    public void SelectFirstWithinLimit_ReturnsFirstMatchingAttempt()
    {
        var result = JpegEncodingPlanner.SelectFirstWithinLimit(
            attempt => attempt switch
            {
                { ScalePercent: 100, Quality: 95 } => AppConstants.MaximumJpegBytes + 1,
                { ScalePercent: 100, Quality: 92 } => AppConstants.MaximumJpegBytes + 1,
                { ScalePercent: 100, Quality: 90 } => AppConstants.MaximumJpegBytes - 10,
                _ => AppConstants.MaximumJpegBytes - 20
            },
            AppConstants.MaximumJpegBytes);

        Assert.Equal(new JpegEncodingAttempt(100, 90), result);
    }
}
