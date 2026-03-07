namespace CandC.HeicClipboard;

public static class AppConstants
{
    public const string ApplicationName = "HeicToClipboard";
    public const string ContextMenuText = "C&C to JPEG";
    public const string MissingHeifSupportMessage = "HEIF/HEIC support is missing in Windows. Install HEIF Image Extensions from Microsoft Store.";
    public const string TempFolderName = "HeicClipboardConvert";
    public const string TempFilePrefix = "HeicToClipboard_";
    public const string MutexName = @"Local\HeicToClipboard_BatchMutex";
    public const string PipeName = "HeicToClipboard_BatchPipe";
    public const int ClipboardRetryCount = 10;
    public const int ClipboardRetryDelayMilliseconds = 100;
    public static readonly long MaximumJpegBytes = (long)Math.Floor(9.8 * 1024 * 1024);
    public static readonly TimeSpan TempFileMaxAge = TimeSpan.FromHours(24);
    public static readonly TimeSpan BatchIdleDelay = TimeSpan.FromMilliseconds(1200);
    public static readonly TimeSpan BatchMaxWait = TimeSpan.FromSeconds(6);
    public static readonly int[] JpegQualitySteps = [95, 92, 90, 88, 85, 82, 80, 75, 70];
    public static readonly int[] DownscalePercentSteps = [95, 90, 85, 80, 75, 70, 65, 60];
}
