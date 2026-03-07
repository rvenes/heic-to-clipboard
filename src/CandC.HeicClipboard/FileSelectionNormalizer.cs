using System.IO;

namespace CandC.HeicClipboard;

public static class FileSelectionNormalizer
{
    public static IReadOnlyList<string> Normalize(IEnumerable<string> rawArguments)
    {
        var uniqueFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<string>();

        foreach (var rawArgument in rawArguments)
        {
            if (string.IsNullOrWhiteSpace(rawArgument))
            {
                continue;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(rawArgument.Trim());
            }
            catch
            {
                continue;
            }

            var extension = Path.GetExtension(fullPath);
            if (!extension.Equals(".heic", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".heif", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (uniqueFiles.Add(fullPath))
            {
                files.Add(fullPath);
            }
        }

        return files;
    }
}
