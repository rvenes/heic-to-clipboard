namespace CandC.HeicClipboard.Tests;

public sealed class JpegEncodingPlannerTests
{
    [Fact]
    public void CreateQualitySteps_StartsAtInitialQualityAndDescends()
    {
        var steps = JpegEncodingPlanner.CreateQualitySteps(AppConstants.DefaultInitialJpegQuality);

        Assert.Equal(95, steps[0]);
        Assert.Equal(AppConstants.MinimumJpegQuality, steps[^1]);
        Assert.Equal(steps.OrderByDescending(static quality => quality), steps);
    }

    [Fact]
    public void CreateQualitySteps_InsertsCustomInitialQualityBeforeBuiltInSteps()
    {
        var steps = JpegEncodingPlanner.CreateQualitySteps(93);

        Assert.Equal(93, steps[0]);
        Assert.Equal(92, steps[1]);
        Assert.DoesNotContain(95, steps);
    }

    [Fact]
    public void CreateQualitySteps_ClampsOutOfRangeInitialQuality()
    {
        Assert.Equal(AppConstants.MaximumJpegQuality, JpegEncodingPlanner.CreateQualitySteps(150)[0]);
        Assert.Equal([AppConstants.MinimumJpegQuality], JpegEncodingPlanner.CreateQualitySteps(10));
    }

    [Fact]
    public void EstimateNextScalePercent_UsesSquareRootOfSizeRatioWithSafetyMargin()
    {
        // 4x over the limit: sqrt(1/4) = 0.5, times the 0.95 safety margin = 47%.
        var next = JpegEncodingPlanner.EstimateNextScalePercent(100, encodedBytes: 4_000_000, maximumBytes: 1_000_000);

        Assert.Equal(47, next);
    }

    [Fact]
    public void EstimateNextScalePercent_CanGoBelowTheOldSixtyPercentFloor()
    {
        // The old grid stopped at 60% scale, which made small limits unreachable.
        var next = JpegEncodingPlanner.EstimateNextScalePercent(100, encodedBytes: 10_000_000, maximumBytes: 150_000);

        Assert.NotNull(next);
        Assert.True(next < 60, $"Expected scale below 60%, got {next}%.");
    }

    [Fact]
    public void EstimateNextScalePercent_AlwaysShrinksByAtLeastOnePercent()
    {
        // Barely over the limit: the raw estimate rounds back to the current scale,
        // so the clamp must force progress.
        var next = JpegEncodingPlanner.EstimateNextScalePercent(100, encodedBytes: 1_000_001, maximumBytes: 1_000_000);

        Assert.NotNull(next);
        Assert.True(next < 100);
    }

    [Fact]
    public void EstimateNextScalePercent_ReturnsNullWhenAlreadyWithinLimit()
    {
        Assert.Null(JpegEncodingPlanner.EstimateNextScalePercent(100, encodedBytes: 500, maximumBytes: 1_000));
    }

    [Fact]
    public void EstimateNextScalePercent_ReturnsNullAtMinimumScale()
    {
        Assert.Null(JpegEncodingPlanner.EstimateNextScalePercent(
            JpegEncodingPlanner.MinimumScalePercent, encodedBytes: 1_000_000, maximumBytes: 1_000));
    }

    [Fact]
    public void EstimateNextScalePercent_IsDeterministic()
    {
        var first = JpegEncodingPlanner.EstimateNextScalePercent(80, 3_000_000, 900_000);
        var second = JpegEncodingPlanner.EstimateNextScalePercent(80, 3_000_000, 900_000);

        Assert.Equal(first, second);
    }

    [Fact]
    public void EstimateSequence_ConvergesWithinMaxAttemptsForRealisticSizes()
    {
        // Simulate a JPEG whose size is proportional to the pixel count
        // (quadratic in scale): 10 MB at 100% with a 150 KB limit.
        const long maximumBytes = 150_000;
        const double bytesAtFullScale = 10_000_000d;
        static long SizeAt(int scalePercent) =>
            (long)(bytesAtFullScale * scalePercent * scalePercent / (100d * 100d));

        var scalePercent = 100;
        var encodedBytes = SizeAt(scalePercent);
        var attemptsUsed = 0;

        for (var attempt = 0; attempt < JpegEncodingPlanner.MaxScaleAttempts; attempt++)
        {
            var next = JpegEncodingPlanner.EstimateNextScalePercent(scalePercent, encodedBytes, maximumBytes);
            if (next is null)
            {
                break;
            }

            scalePercent = next.Value;
            encodedBytes = SizeAt(scalePercent);
            attemptsUsed++;

            if (encodedBytes <= maximumBytes)
            {
                break;
            }
        }

        Assert.True(encodedBytes <= maximumBytes, $"Did not converge: {encodedBytes} bytes at {scalePercent}%.");
        Assert.True(attemptsUsed <= 2, $"Expected convergence within 2 attempts, used {attemptsUsed}.");
    }

    [Fact]
    public void EstimateSequence_TerminatesQuicklyForImpossibleTargets()
    {
        // A size model that never fits: even a 1x1 image is larger than the limit.
        const long maximumBytes = 10;
        var scalePercent = 100;
        var iterations = 0;

        while (iterations < 100)
        {
            var next = JpegEncodingPlanner.EstimateNextScalePercent(scalePercent, encodedBytes: 5_000, maximumBytes: maximumBytes);
            if (next is null)
            {
                break;
            }

            Assert.True(next < scalePercent, "Scale must shrink monotonically.");
            scalePercent = next.Value;
            iterations++;
        }

        Assert.True(iterations < 100, "Estimate sequence did not terminate.");
        Assert.Equal(JpegEncodingPlanner.MinimumScalePercent, scalePercent);
    }

    [Fact]
    public void QualityLadder_WalkedInOrder_FindsHighestQualityThatFits()
    {
        // Regression guard: even when the first attempt overshoots the limit by a lot,
        // the ladder must not skip ahead to the floor quality; the first (highest)
        // fitting step wins. Here 95 is 5x over the limit but 85 already fits.
        const long maximumBytes = 1_000_000;
        static long SizeAt(int quality) => quality switch
        {
            >= 92 => 5_000_000,
            >= 88 => 1_200_000,
            _ => 900_000
        };

        var chosenQuality = JpegEncodingPlanner
            .CreateQualitySteps(AppConstants.DefaultInitialJpegQuality)
            .First(quality => SizeAt(quality) <= maximumBytes);

        Assert.Equal(85, chosenQuality);
    }
}
