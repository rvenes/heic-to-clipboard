namespace CandC.HeicClipboard.Tests;

public sealed class SummaryFormatterTests
{
    [Fact]
    public void Format_IncludesCountsFailuresAndClipboardErrors()
    {
        var result = new BatchProcessResult(
            new[]
            {
                ConversionResult.Succeeded(@"C:\Images\ok.heic", @"C:\Temp\ok.jpg"),
                ConversionResult.Failed(@"C:\Images\bad.heic", "The file could not be decoded.")
            },
            clipboardUpdated: false,
            clipboardError: "Clipboard update failed: Access denied.");

        var text = SummaryFormatter.Format(result);

        Assert.Contains("Processed: 2", text);
        Assert.Contains("Succeeded: 1", text);
        Assert.Contains("Failed: 1", text);
        Assert.Contains("Clipboard update failed: Access denied.", text);
        Assert.Contains("bad.heic: The file could not be decoded.", text);
    }
}
