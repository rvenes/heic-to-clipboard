namespace CandC.HeicClipboard;

public static class OutputPathResolver
{
    public static OutputDirectoryOptions Resolve(HeicToClipboardSettings settings, string tempDirectory)
    {
        var sanitized = settings.Sanitize();
        if (sanitized.UseCustomOutputFolder && !string.IsNullOrWhiteSpace(sanitized.CustomOutputFolder))
        {
            try
            {
                Directory.CreateDirectory(sanitized.CustomOutputFolder);
                return new OutputDirectoryOptions(sanitized.CustomOutputFolder, false, TimeSpan.Zero);
            }
            catch
            {
            }
        }

        Directory.CreateDirectory(tempDirectory);
        return new OutputDirectoryOptions(tempDirectory, true, TimeSpan.FromDays(sanitized.TempCleanupDays));
    }
}

public readonly record struct OutputDirectoryOptions(string WorkingDirectory, bool CleanupEnabled, TimeSpan CleanupAge);
