using System.Globalization;

namespace CandC.HeicClipboard;

public sealed record HeicConversionOptions(long MaximumBytes, int InitialJpegQuality, bool KeepOriginalResolution, int? MaxLongestSidePx)
{
    public string SizeLimitExceededMessage
    {
        get
        {
            var megabytes = MaximumBytes / (1024d * 1024d);
            return megabytes >= 0.01
                ? string.Create(CultureInfo.InvariantCulture, $"Could not keep the JPEG under {megabytes:0.##} MB.")
                : string.Create(CultureInfo.InvariantCulture, $"Could not keep the JPEG under {MaximumBytes} bytes.");
        }
    }

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
