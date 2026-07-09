using System.Windows.Forms;

namespace CandC.HeicClipboard;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Unexpected error: {exception.Message}",
                AppConstants.ApplicationName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var settingsStore = new HeicToClipboardSettingsStore(Path.Combine(AppContext.BaseDirectory, AppConstants.SettingsFileName));

        if (args.Length == 0)
        {
            using var settingsForm = new SettingsForm(settingsStore.Load(), settingsStore, AppConstants.DefaultTempDirectory);
            settingsForm.ShowDialog();
            return 0;
        }

        var normalizedFiles = FileSelectionNormalizer.Normalize(args);
        if (normalizedFiles.Count == 0)
        {
            return 0;
        }

        using var coordinator = InvocationCoordinator.CreateDefault();
        if (coordinator.CollectOrForward(normalizedFiles) == CoordinatorRole.Forwarded)
        {
            return 0;
        }

        var settings = settingsStore.Load();
        var outputOptions = OutputPathResolver.Resolve(settings, AppConstants.DefaultTempDirectory);
        var tempFileManager = new TempFileManager(outputOptions.WorkingDirectory, outputOptions.CleanupAge, outputOptions.CleanupEnabled);
        tempFileManager.CleanupExpiredFiles();

        var processor = new BatchProcessor(new HeicConverter(tempFileManager, HeicConversionOptions.FromSettings(settings)), new ClipboardService());
        var result = processor.Process(coordinator);

        // Release the mutex and pipe before any modal summary, so a new invocation
        // made while the dialog is open can start its own batch.
        coordinator.Dispose();

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
