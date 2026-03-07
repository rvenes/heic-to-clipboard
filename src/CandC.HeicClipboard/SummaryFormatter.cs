using System.IO;

namespace CandC.HeicClipboard;

public static class SummaryFormatter
{
    public static string Format(BatchProcessResult result)
    {
        var lines = new List<string>
        {
            $"Processed: {result.ProcessedCount}",
            $"Succeeded: {result.SuccessCount}",
            $"Failed: {result.FailureCount}"
        };

        if (!string.IsNullOrWhiteSpace(result.ClipboardError))
        {
            lines.Add(string.Empty);
            lines.Add(result.ClipboardError);
        }

        if (result.FailedResults.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Failed files:");

            foreach (var failure in result.FailedResults)
            {
                lines.Add($"- {Path.GetFileName(failure.SourcePath)}: {failure.ErrorMessage}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}
