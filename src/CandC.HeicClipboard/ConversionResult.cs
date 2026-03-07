namespace CandC.HeicClipboard;

public sealed record ConversionResult(string SourcePath, bool Success, string? OutputPath, string? ErrorMessage)
{
    public static ConversionResult Succeeded(string sourcePath, string outputPath) =>
        new(sourcePath, true, outputPath, null);

    public static ConversionResult Failed(string sourcePath, string errorMessage) =>
        new(sourcePath, false, null, errorMessage);
}
