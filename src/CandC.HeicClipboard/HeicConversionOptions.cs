namespace CandC.HeicClipboard;

public sealed record HeicConversionOptions(long MaximumBytes, int InitialJpegQuality, bool KeepOriginalResolution, int? MaxLongestSidePx)
{
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
