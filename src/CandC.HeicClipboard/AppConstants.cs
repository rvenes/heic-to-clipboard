namespace CandC.HeicClipboard;

public static class AppConstants
{
    public const string ApplicationName = "HeicToClipboard";
    public const string ContextMenuText = "C&C to JPEG";
    public const string MissingHeifSupportMessage = "HEIF/HEIC support is missing in Windows. Install HEIF Image Extensions from Microsoft Store.";
    public const string SettingsFileName = "settings.json";
    public const string TempFolderName = "HeicClipboardConvert";
    public const string TempFilePrefix = "HeicToClipboard_";
    public const string MutexName = @"Local\HeicToClipboard_BatchMutex";
    public const string PipeName = "HeicToClipboard_BatchPipe";
    public const int ClipboardRetryCount = 10;
    public const int ClipboardRetryDelayMilliseconds = 100;
    public const decimal DefaultMaximumFileSizeMb = 9.8m;
    public const int DefaultInitialJpegQuality = 95;
    public const int MinimumJpegQuality = 70;
    public const int MaximumJpegQuality = 100;
    public const int DefaultTempCleanupDays = 1;
    public static readonly long MaximumJpegBytes = ToBytes(DefaultMaximumFileSizeMb);
    public static readonly TimeSpan TempFileMaxAge = TimeSpan.FromDays(DefaultTempCleanupDays);
    public static readonly TimeSpan BatchIdleDelay = TimeSpan.FromMilliseconds(1200);
    public static readonly TimeSpan BatchMaxWait = TimeSpan.FromSeconds(6);
    public static readonly int[] JpegQualitySteps = [95, 92, 90, 88, 85, 82, 80, 75, 70];
    public static readonly int[] DownscalePercentSteps = [95, 90, 85, 80, 75, 70, 65, 60];

    public static string DefaultTempDirectory => Path.Combine(Path.GetTempPath(), TempFolderName);

    public static long ToBytes(decimal megabytes) => (long)Math.Floor((double)(megabytes * 1024m * 1024m));
}
