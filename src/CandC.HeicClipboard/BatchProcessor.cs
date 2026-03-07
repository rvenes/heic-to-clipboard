namespace CandC.HeicClipboard;

public sealed class BatchProcessor
{
    private readonly HeicConverter _converter;
    private readonly ClipboardService _clipboardService;

    public BatchProcessor(HeicConverter converter, ClipboardService clipboardService)
    {
        _converter = converter;
        _clipboardService = clipboardService;
    }

    public BatchProcessResult Process(IReadOnlyList<string> files)
    {
        var results = new List<ConversionResult>(files.Count);
        foreach (var file in files)
        {
            results.Add(_converter.Convert(file));
        }

        var successfulFiles = results
            .Where(static result => result.Success && result.OutputPath is not null)
            .Select(static result => result.OutputPath!)
            .ToArray();

        if (successfulFiles.Length == 0)
        {
            return new BatchProcessResult(results, clipboardUpdated: false, clipboardError: null);
        }

        var singleImagePath = successfulFiles.Length == 1 ? successfulFiles[0] : null;
        var clipboardUpdated = _clipboardService.TrySetFiles(successfulFiles, singleImagePath, out var clipboardError);

        return new BatchProcessResult(results, clipboardUpdated, clipboardError);
    }
}
