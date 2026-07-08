namespace CandC.HeicClipboard;

public interface IImageConverter
{
    ConversionResult Convert(string sourcePath);
}

public sealed class BatchProcessor
{
    private readonly IImageConverter _converter;
    private readonly ClipboardService _clipboardService;

    public BatchProcessor(IImageConverter converter, ClipboardService clipboardService)
    {
        _converter = converter;
        _clipboardService = clipboardService;
    }

    public BatchProcessResult Process(IFileBatchSource batchSource)
    {
        var results = new List<ConversionResult>();
        var clipboardUpdated = false;
        string? clipboardError = null;

        var batch = batchSource.WaitForFirstBatch();
        while (batch.Count > 0)
        {
            foreach (var file in batch)
            {
                results.Add(_converter.Convert(file));
            }

            // Rewrite the clipboard with the cumulative set after every round, so
            // stragglers extend the paste selection instead of replacing it.
            var successfulFiles = results
                .Where(static result => result.Success && result.OutputPath is not null)
                .Select(static result => result.OutputPath!)
                .ToArray();

            if (successfulFiles.Length > 0)
            {
                clipboardUpdated = _clipboardService.TrySetFiles(successfulFiles, out clipboardError);
            }

            batch = batchSource.WaitForAdditionalFiles();
        }

        return new BatchProcessResult(results, clipboardUpdated, clipboardError);
    }
}
