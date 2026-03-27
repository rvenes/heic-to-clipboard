using System.Collections.Specialized;
using System.Windows.Forms;

namespace CandC.HeicClipboard;

public sealed class ClipboardService
{
    private readonly Action<DataObject> _clipboardWriter;

    public ClipboardService(Action<DataObject>? clipboardWriter = null)
    {
        _clipboardWriter = clipboardWriter ?? (dataObject =>
            Clipboard.SetDataObject(dataObject, true, AppConstants.ClipboardRetryCount, AppConstants.ClipboardRetryDelayMilliseconds));
    }

    public bool TrySetFiles(IReadOnlyList<string> filePaths, string? singleImagePath, out string? errorMessage)
    {
        errorMessage = null;

        try
        {
            var fileDropDataObject = CreateFileDropDataObject(filePaths);
            _clipboardWriter(fileDropDataObject);
            return true;
        }
        catch (Exception exception)
        {
            errorMessage = $"Clipboard update failed: {exception.Message}";
            return false;
        }
    }

    private static DataObject CreateFileDropDataObject(IReadOnlyList<string> filePaths)
    {
        var dropList = new StringCollection();
        dropList.AddRange(filePaths.ToArray());

        var dataObject = new DataObject();
        dataObject.SetFileDropList(dropList);
        return dataObject;
    }
}
