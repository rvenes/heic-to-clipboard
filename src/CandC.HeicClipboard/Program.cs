using System.Windows.Forms;

namespace CandC.HeicClipboard;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (args.Length == 0)
        {
            return 0;
        }

        var invocationBatch = InvocationCoordinator.Collect(args);
        if (invocationBatch.ShouldExitCurrentInstance || invocationBatch.Files.Count == 0)
        {
            return 0;
        }

        var tempFileManager = new TempFileManager();
        tempFileManager.CleanupExpiredFiles();

        var processor = new BatchProcessor(new HeicConverter(tempFileManager), new ClipboardService());
        var result = processor.Process(invocationBatch.Files);

        if (result.ShouldShowMessage)
        {
            var icon = result.HasSuccessfulClipboardUpdate ? MessageBoxIcon.Warning : MessageBoxIcon.Error;
            MessageBox.Show(
                SummaryFormatter.Format(result),
                AppConstants.ApplicationName,
                MessageBoxButtons.OK,
                icon);
        }

        return result.HasSuccessfulClipboardUpdate ? 0 : 1;
    }
}
