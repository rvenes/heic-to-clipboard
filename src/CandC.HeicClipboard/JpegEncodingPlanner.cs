namespace CandC.HeicClipboard;

/// <summary>
/// Plans the JPEG encoding attempts in two phases: first descending quality at full
/// scale (so the common case behaves as before), then downscaling by a scale factor
/// estimated from the last encoded size. JPEG size is roughly proportional to the
/// pixel count at a fixed quality, so sqrt(limit/actual) predicts the scale that
/// lands just under the limit; a safety factor absorbs the approximation error.
/// </summary>
public static class JpegEncodingPlanner
{
    public const int MaxScaleAttempts = 5;
    public const int MinimumScalePercent = 1;

    private const double ScaleSafetyFactor = 0.95;

    public static IReadOnlyList<int> CreateQualitySteps(int initialQuality)
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

    public static int? EstimateNextScalePercent(int currentScalePercent, long encodedBytes, long maximumBytes)
    {
        if (currentScalePercent <= MinimumScalePercent || encodedBytes <= maximumBytes || maximumBytes <= 0)
        {
            return null;
        }

        var ratio = Math.Sqrt(maximumBytes / (double)encodedBytes) * ScaleSafetyFactor;
        var nextScalePercent = (int)Math.Floor(currentScalePercent * ratio);

        // Always shrink by at least one percent so the attempt sequence terminates.
        return Math.Clamp(nextScalePercent, MinimumScalePercent, currentScalePercent - 1);
    }
}
