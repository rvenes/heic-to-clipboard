using System.Collections.Specialized;
using System.Drawing;
using System.Windows.Forms;

namespace CandC.HeicClipboard;

public sealed class ClipboardService
{
    public bool TrySetFiles(IReadOnlyList<string> filePaths, string? singleImagePath, out string? errorMessage)
    {
        errorMessage = null;

        try
        {
            var dropList = new StringCollection();
            dropList.AddRange(filePaths.ToArray());

            var dataObject = new DataObject();
            dataObject.SetFileDropList(dropList);

            if (filePaths.Count == 1 && !string.IsNullOrWhiteSpace(singleImagePath))
            {
                using var sourceImage = Image.FromFile(singleImagePath);
                using var clipboardImage = new Bitmap(sourceImage);
                dataObject.SetData(DataFormats.Bitmap, true, clipboardImage);
                Clipboard.SetDataObject(dataObject, true, AppConstants.ClipboardRetryCount, AppConstants.ClipboardRetryDelayMilliseconds);
                return true;
            }

            Clipboard.SetDataObject(dataObject, true, AppConstants.ClipboardRetryCount, AppConstants.ClipboardRetryDelayMilliseconds);
            return true;
        }
        catch (Exception exception)
        {
            errorMessage = $"Clipboard update failed: {exception.Message}";
            return false;
        }
    }
}
