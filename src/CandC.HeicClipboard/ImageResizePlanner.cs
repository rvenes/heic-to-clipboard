namespace CandC.HeicClipboard;

public static class ImageResizePlanner
{
    public static ImageDimensions FitWithinLongestSide(int width, int height, int? maxLongestSidePx)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (maxLongestSidePx is not > 0)
        {
            return new ImageDimensions(width, height);
        }

        var currentLongestSide = Math.Max(width, height);
        if (currentLongestSide <= maxLongestSidePx.Value)
        {
            return new ImageDimensions(width, height);
        }

        var scale = maxLongestSidePx.Value / (double)currentLongestSide;
        var fittedWidth = Math.Max(1, (int)Math.Round(width * scale));
        var fittedHeight = Math.Max(1, (int)Math.Round(height * scale));
        return new ImageDimensions(fittedWidth, fittedHeight);
    }
}

public readonly record struct ImageDimensions(int Width, int Height);
