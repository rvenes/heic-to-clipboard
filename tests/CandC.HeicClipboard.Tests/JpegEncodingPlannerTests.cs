namespace CandC.HeicClipboard.Tests;

public sealed class JpegEncodingPlannerTests
{
    [Fact]
    public void CreateAttempts_StartsWithOriginalScaleQualitySteps()
    {
        var attempts = JpegEncodingPlanner.CreateAttempts(AppConstants.DefaultInitialJpegQuality);

        Assert.Equal(new JpegEncodingAttempt(100, 95), attempts[0]);
        Assert.Equal(new JpegEncodingAttempt(100, 92), attempts[1]);
        Assert.Equal(new JpegEncodingAttempt(100, 90), attempts[2]);
    }

    [Fact]
    public void CreateAttempts_IncludesCustomInitialQualityBeforeBuiltInSteps()
    {
        var attempts = JpegEncodingPlanner.CreateAttempts(93);

        Assert.Equal(new JpegEncodingAttempt(100, 93), attempts[0]);
        Assert.Equal(new JpegEncodingAttempt(100, 92), attempts[1]);
        Assert.Equal(new JpegEncodingAttempt(100, 90), attempts[2]);
    }
}
