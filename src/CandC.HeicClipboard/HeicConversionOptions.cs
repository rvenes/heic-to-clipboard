using System.Globalization;

namespace CandC.HeicClipboard;

public sealed record HeicConversionOptions(long MaximumBytes, int InitialJpegQuality, bool KeepOriginalResolution, int? MaxLongestSidePx)
{
    public string SizeLimitExceededMessage =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Could not keep the JPEG under {MaximumBytes / (1024d * 1024d):0.##} MB.");

    public static HeicConversionOptions FromSettings(HeicToClipboardSettings settings)
    {
        var sanitized = settings.Sanitize();
        return new HeicConversionOptions(
            AppConstants.ToBytes(sanitized.MaxFileSizeMb),
            sanitized.InitialJpegQuality,
            sanitized.KeepOriginalResolution,
            sanitized.MaxLongestSidePx);
    }
}
