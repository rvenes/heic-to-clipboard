namespace CandC.HeicClipboard.Tests;

internal static class LocalHeicSamples
{
    public const string SamplesDirectory = @"H:\Koding\CandC-Samples";

    public static IReadOnlyList<string> GetFiles()
    {
        if (!Directory.Exists(SamplesDirectory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(SamplesDirectory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(static path =>
            {
                var extension = Path.GetExtension(path);
                return extension.Equals(".heic", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".heif", StringComparison.OrdinalIgnoreCase);
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
