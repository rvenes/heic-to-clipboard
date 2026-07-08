namespace CandC.HeicClipboard;

public static class JpegEncodingPlanner
{
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
