using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CandC.HeicClipboard.Tests;

public sealed class ClipboardServiceTests
{
    [Fact]
    public void TrySetFiles_WritesFileDropData()
    {
        DataObject? writtenDataObject = null;
        var service = new ClipboardService(clipboardWriter: dataObject => writtenDataObject = dataObject);

        var updated = service.TrySetFiles(["C:\\Temp\\image.jpg"], "C:\\Temp\\image.jpg", out var errorMessage);

        Assert.True(updated);
        Assert.Null(errorMessage);
        Assert.NotNull(writtenDataObject);
        Assert.True(writtenDataObject!.ContainsFileDropList());
        Assert.Null(writtenDataObject.GetData(DataFormats.Bitmap));
    }

    [Fact]
    public void TrySetFiles_ReturnsFailureWhenClipboardWriteFails()
    {
        var service = new ClipboardService(
            clipboardWriter: _ => throw new ExternalException("Requested Clipboard operation did not succeed."));

        var updated = service.TrySetFiles(["C:\\Temp\\image.jpg"], "C:\\Temp\\image.jpg", out var errorMessage);

        Assert.False(updated);
        Assert.Equal("Clipboard update failed: Requested Clipboard operation did not succeed.", errorMessage);
    }
}
