namespace CandC.HeicClipboard;

public sealed class BatchProcessResult
{
    public BatchProcessResult(IReadOnlyList<ConversionResult> results, bool clipboardUpdated, string? clipboardError)
    {
        Results = results;
        ClipboardUpdated = clipboardUpdated;
        ClipboardError = clipboardError;
    }

    public IReadOnlyList<ConversionResult> Results { get; }

    public bool ClipboardUpdated { get; }

    public string? ClipboardError { get; }

    public int ProcessedCount => Results.Count;

    public int SuccessCount => Results.Count(static result => result.Success);

    public int FailureCount => ProcessedCount - SuccessCount;

    public bool HasSuccessfulClipboardUpdate => SuccessCount > 0 && ClipboardUpdated;

    public bool ShouldShowMessage => FailureCount > 0 || (SuccessCount > 0 && !ClipboardUpdated);

    public IReadOnlyList<ConversionResult> FailedResults =>
        Results.Where(static result => !result.Success).ToArray();

    public IReadOnlyList<ConversionResult> SuccessfulResults =>
        Results.Where(static result => result.Success).ToArray();
}
