namespace CandC.HeicClipboard.Tests;

public sealed class FileSelectionNormalizerTests
{
    [Fact]
    public void Normalize_FiltersDuplicatesAndNonHeifFiles()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var input = new[]
        {
            @".\photo.heic",
            @".\PHOTO.HEIC",
            @".\clip.heif",
            @".\note.txt"
        };

        var normalized = FileSelectionNormalizer.Normalize(input);

        Assert.Equal(2, normalized.Count);
        Assert.Contains(Path.Combine(currentDirectory, "photo.heic"), normalized, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine(currentDirectory, "clip.heif"), normalized, StringComparer.OrdinalIgnoreCase);
    }
}
