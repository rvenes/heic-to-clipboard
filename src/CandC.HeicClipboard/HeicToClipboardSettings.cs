namespace CandC.HeicClipboard;

public sealed class HeicToClipboardSettings
{
    public bool UseCustomOutputFolder { get; set; }

    public string CustomOutputFolder { get; set; } = string.Empty;

    public decimal MaxFileSizeMb { get; set; } = AppConstants.DefaultMaximumFileSizeMb;

    public int InitialJpegQuality { get; set; } = AppConstants.DefaultInitialJpegQuality;

    public bool KeepOriginalResolution { get; set; } = true;

    public int? MaxLongestSidePx { get; set; }

    public int TempCleanupDays { get; set; } = AppConstants.DefaultTempCleanupDays;

    public static HeicToClipboardSettings CreateDefault() => new();

    public HeicToClipboardSettings Sanitize()
    {
        var maxLongestSidePx = MaxLongestSidePx is > 0 ? MaxLongestSidePx : null;
        var keepOriginalResolution = maxLongestSidePx.HasValue ? false : KeepOriginalResolution;

        return new HeicToClipboardSettings
        {
            UseCustomOutputFolder = UseCustomOutputFolder,
            CustomOutputFolder = (CustomOutputFolder ?? string.Empty).Trim(),
            MaxFileSizeMb = MaxFileSizeMb > 0 ? MaxFileSizeMb : AppConstants.DefaultMaximumFileSizeMb,
            InitialJpegQuality = Math.Clamp(InitialJpegQuality, AppConstants.MinimumJpegQuality, AppConstants.MaximumJpegQuality),
            KeepOriginalResolution = keepOriginalResolution,
            MaxLongestSidePx = maxLongestSidePx,
            TempCleanupDays = TempCleanupDays >= 1 ? TempCleanupDays : AppConstants.DefaultTempCleanupDays
        };
    }
}
