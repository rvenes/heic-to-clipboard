namespace CandC.HeicClipboard;

public static class JpegEncodingPlanner
{
    public static IReadOnlyList<JpegEncodingAttempt> CreateAttempts()
    {
        return CreateAttempts(AppConstants.DefaultInitialJpegQuality);
    }

    public static IReadOnlyList<JpegEncodingAttempt> CreateAttempts(int initialQuality)
    {
        var attempts = new List<JpegEncodingAttempt>();
        var qualitySteps = CreateQualitySteps(initialQuality);

        foreach (var quality in qualitySteps)
        {
            attempts.Add(new JpegEncodingAttempt(100, quality));
        }

        foreach (var scalePercent in AppConstants.DownscalePercentSteps)
        {
            foreach (var quality in qualitySteps)
            {
                attempts.Add(new JpegEncodingAttempt(scalePercent, quality));
            }
        }

        return attempts;
    }

    public static JpegEncodingAttempt? SelectFirstWithinLimit(Func<JpegEncodingAttempt, long> sizeEvaluator, long maximumBytes)
    {
        return SelectFirstWithinLimit(sizeEvaluator, maximumBytes, AppConstants.DefaultInitialJpegQuality);
    }

    public static JpegEncodingAttempt? SelectFirstWithinLimit(Func<JpegEncodingAttempt, long> sizeEvaluator, long maximumBytes, int initialQuality)
    {
        foreach (var attempt in CreateAttempts(initialQuality))
        {
            if (sizeEvaluator(attempt) <= maximumBytes)
            {
                return attempt;
            }
        }

        return null;
    }

    private static IReadOnlyList<int> CreateQualitySteps(int initialQuality)
    {
        var clampedInitialQuality = Math.Clamp(initialQuality, AppConstants.MinimumJpegQuality, AppConstants.MaximumJpegQuality);
        var qualities = new List<int> { clampedInitialQuality };

        foreach (var quality in AppConstants.JpegQualitySteps)
        {
            if (quality <= clampedInitialQuality && !qualities.Contains(quality))
            {
                qualities.Add(quality);
            }
        }

        return qualities;
    }
}

public readonly record struct JpegEncodingAttempt(int ScalePercent, int Quality);
