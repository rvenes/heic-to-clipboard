namespace CandC.HeicClipboard;

public static class OutputPathResolver
{
    public static OutputDirectoryOptions Resolve(HeicToClipboardSettings settings, string tempDirectory)
    {
        var sanitized = settings.Sanitize();
        if (sanitized.UseCustomOutputFolder)
        {
            if (string.IsNullOrWhiteSpace(sanitized.CustomOutputFolder))
            {
                throw new IOException("No custom output folder is selected. Choose a folder in Settings or disable custom output.");
            }

            try
            {
                Directory.CreateDirectory(sanitized.CustomOutputFolder);
                return new OutputDirectoryOptions(sanitized.CustomOutputFolder, false, TimeSpan.Zero);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                throw new IOException(
                    $"The selected output folder is unavailable: {sanitized.CustomOutputFolder}.{Environment.NewLine}" +
                    "No files were converted. Check the folder or choose another output folder in Settings.", exception);
            }
        }

        Directory.CreateDirectory(tempDirectory);
        return new OutputDirectoryOptions(tempDirectory, true, TimeSpan.FromDays(sanitized.TempCleanupDays));
    }
}

public readonly record struct OutputDirectoryOptions(string WorkingDirectory, bool CleanupEnabled, TimeSpan CleanupAge);
