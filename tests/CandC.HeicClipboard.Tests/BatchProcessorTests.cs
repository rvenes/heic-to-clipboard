using System.Windows.Forms;

namespace CandC.HeicClipboard.Tests;

public sealed class BatchProcessorTests
{
    private sealed class StubConverter : IImageConverter
    {
        public List<string> ConvertedFiles { get; } = [];

        public ConversionResult Convert(string sourcePath)
        {
            ConvertedFiles.Add(sourcePath);
            return sourcePath.Contains("bad")
                ? ConversionResult.Failed(sourcePath, "Decode failed.")
                : ConversionResult.Succeeded(sourcePath, sourcePath + ".jpg");
        }
    }

    private sealed class FakeBatchSource : IFileBatchSource
    {
        private readonly Queue<IReadOnlyList<string>> _rounds;

        public FakeBatchSource(params IReadOnlyList<string>[] rounds)
        {
            _rounds = new Queue<IReadOnlyList<string>>(rounds);
        }

        public IReadOnlyList<string> WaitForFirstBatch() => WaitForAdditionalFiles();

        public IReadOnlyList<string> WaitForAdditionalFiles() =>
            _rounds.Count > 0 ? _rounds.Dequeue() : Array.Empty<string>();
    }

    private static (BatchProcessor Processor, StubConverter Converter, List<string[]> ClipboardWrites) CreateProcessor()
    {
        var converter = new StubConverter();
        var clipboardWrites = new List<string[]>();
        var clipboardService = new ClipboardService(dataObject =>
        {
            var dropList = dataObject.GetFileDropList();
            var paths = new string[dropList.Count];
            dropList.CopyTo(paths, 0);
            clipboardWrites.Add(paths);
        });

        return (new BatchProcessor(converter, clipboardService), converter, clipboardWrites);
    }

    [Fact]
    public void Process_SingleRound_ConvertsAllAndSetsClipboardOnce()
    {
        var (processor, converter, clipboardWrites) = CreateProcessor();
        var source = new FakeBatchSource([@"C:\a.heic", @"C:\b.heic"]);

        var result = processor.Process(source);

        Assert.Equal(2, result.SuccessCount);
        Assert.True(result.ClipboardUpdated);
        Assert.Equal([@"C:\a.heic", @"C:\b.heic"], converter.ConvertedFiles);
        Assert.Single(clipboardWrites);
        Assert.Equal([@"C:\a.heic.jpg", @"C:\b.heic.jpg"], clipboardWrites[0]);
    }

    [Fact]
    public void Process_StragglerRound_RewritesClipboardWithCumulativeSet()
    {
        var (processor, _, clipboardWrites) = CreateProcessor();
        var source = new FakeBatchSource(
            [@"C:\first.heic"],
            [@"C:\late.heic"]);

        var result = processor.Process(source);

        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(2, clipboardWrites.Count);
        Assert.Equal([@"C:\first.heic.jpg"], clipboardWrites[0]);
        // The second write must contain the whole batch, not just the straggler.
        Assert.Equal([@"C:\first.heic.jpg", @"C:\late.heic.jpg"], clipboardWrites[1]);
    }

    [Fact]
    public void Process_FailedFilesAreReportedButExcludedFromClipboard()
    {
        var (processor, _, clipboardWrites) = CreateProcessor();
        var source = new FakeBatchSource([@"C:\good.heic", @"C:\bad.heic"]);

        var result = processor.Process(source);

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
        Assert.True(result.ClipboardUpdated);
        Assert.Equal([@"C:\good.heic.jpg"], clipboardWrites.Single());
    }

    [Fact]
    public void Process_AllFilesFail_DoesNotTouchClipboard()
    {
        var (processor, _, clipboardWrites) = CreateProcessor();
        var source = new FakeBatchSource([@"C:\bad.heic"]);

        var result = processor.Process(source);

        Assert.False(result.ClipboardUpdated);
        Assert.True(result.ShouldShowMessage);
        Assert.Empty(clipboardWrites);
    }
}
