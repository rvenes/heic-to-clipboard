using System.IO;

namespace CandC.HeicClipboard;

public sealed class TempFileManager
{
    public TempFileManager(string? workingDirectory = null)
    {
        WorkingDirectory = workingDirectory ?? Path.Combine(Path.GetTempPath(), AppConstants.TempFolderName);
        Directory.CreateDirectory(WorkingDirectory);
    }

    public string WorkingDirectory { get; }

    public void CleanupExpiredFiles()
    {
        foreach (var expiredFile in GetExpiredFiles(DateTime.UtcNow))
        {
            try
            {
                File.Delete(expiredFile);
            }
            catch
            {
            }
        }
    }

    public IReadOnlyList<string> GetExpiredFiles(DateTime utcNow)
    {
        if (!Directory.Exists(WorkingDirectory))
        {
            return Array.Empty<string>();
        }

        var expirationThreshold = utcNow - AppConstants.TempFileMaxAge;
        return Directory
            .EnumerateFiles(WorkingDirectory, $"{AppConstants.TempFilePrefix}*.*", SearchOption.TopDirectoryOnly)
            .Where(path => File.GetLastWriteTimeUtc(path) < expirationThreshold)
            .ToArray();
    }

    public string CreateOutputPath(string sourcePath)
    {
        var baseName = Sanitize(Path.GetFileNameWithoutExtension(sourcePath));
        var token = Guid.NewGuid().ToString("N")[..8];
        var fileName = $"{AppConstants.TempFilePrefix}{baseName}_{DateTime.UtcNow:yyyyMMddHHmmss}_{token}.jpg";
        return Path.Combine(WorkingDirectory, fileName);
    }

    private static string Sanitize(string value)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalidChar, '_');
        }

        return string.IsNullOrWhiteSpace(value) ? "image" : value;
    }
}
