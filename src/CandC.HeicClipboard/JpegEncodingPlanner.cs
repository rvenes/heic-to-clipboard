namespace CandC.HeicClipboard;

public static class JpegEncodingPlanner
{
    public static IReadOnlyList<JpegEncodingAttempt> CreateAttempts()
    {
        var attempts = new List<JpegEncodingAttempt>();

        foreach (var quality in AppConstants.JpegQualitySteps)
        {
            attempts.Add(new JpegEncodingAttempt(100, quality));
        }

        foreach (var scalePercent in AppConstants.DownscalePercentSteps)
        {
            foreach (var quality in AppConstants.JpegQualitySteps)
            {
                attempts.Add(new JpegEncodingAttempt(scalePercent, quality));
            }
        }

        return attempts;
    }

    public static JpegEncodingAttempt? SelectFirstWithinLimit(Func<JpegEncodingAttempt, long> sizeEvaluator, long maximumBytes)
    {
        foreach (var attempt in CreateAttempts())
        {
            if (sizeEvaluator(attempt) <= maximumBytes)
            {
                return attempt;
            }
        }

        return null;
    }
}

public readonly record struct JpegEncodingAttempt(int ScalePercent, int Quality);
