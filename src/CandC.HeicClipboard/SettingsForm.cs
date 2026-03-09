using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace CandC.HeicClipboard;

public sealed class SettingsForm : Form
{
    private readonly HeicToClipboardSettingsStore _settingsStore;
    private readonly string _tempDirectory;
    private readonly TextBox _tempFolderTextBox;
    private readonly CheckBox _useCustomFolderCheckBox;
    private readonly TextBox _customFolderTextBox;
    private readonly Button _browseButton;
    private readonly NumericUpDown _maxFileSizeNumeric;
    private readonly TrackBar _qualitySlider;
    private readonly Label _qualityValueLabel;
    private readonly CheckBox _keepOriginalResolutionCheckBox;
    private readonly TextBox _maxLongestSideTextBox;
    private readonly NumericUpDown _cleanupDaysNumeric;

    public SettingsForm(HeicToClipboardSettings settings, HeicToClipboardSettingsStore settingsStore, string tempDirectory)
    {
        _settingsStore = settingsStore;
        _tempDirectory = tempDirectory;

        Text = $"{AppConstants.ApplicationName} Settings";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(780, 560);
        MinimumSize = new Size(780, 560);

        _tempFolderTextBox = CreateReadOnlyTextBox();
        _useCustomFolderCheckBox = new CheckBox
        {
            Text = "Use custom output folder",
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 4)
        };
        _useCustomFolderCheckBox.CheckedChanged += (_, _) => UpdateCustomFolderState();

        _customFolderTextBox = CreateTextBox();
        _browseButton = CreateSmallButton("Browse...");
        _browseButton.Click += (_, _) => BrowseForFolder();

        _maxFileSizeNumeric = new NumericUpDown
        {
            DecimalPlaces = 1,
            Increment = 0.1m,
            Minimum = 0.1m,
            Maximum = 500m,
            Width = 110
        };

        _qualitySlider = new TrackBar
        {
            Minimum = AppConstants.MinimumJpegQuality,
            Maximum = AppConstants.MaximumJpegQuality,
            TickFrequency = 5,
            SmallChange = 1,
            LargeChange = 5,
            AutoSize = false,
            Height = 40,
            Dock = DockStyle.Fill
        };

        _qualityValueLabel = new Label
        {
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Anchor = AnchorStyles.Left,
            MinimumSize = new Size(42, 0)
        };
        _qualitySlider.ValueChanged += (_, _) => _qualityValueLabel.Text = $"{_qualitySlider.Value}%";

        _keepOriginalResolutionCheckBox = new CheckBox
        {
            Text = "Keep original resolution",
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 4)
        };
        _keepOriginalResolutionCheckBox.CheckedChanged += (_, _) => UpdateResolutionState();

        _maxLongestSideTextBox = CreateTextBox();
        _maxLongestSideTextBox.Width = 110;

        _cleanupDaysNumeric = new NumericUpDown
        {
            DecimalPlaces = 0,
            Increment = 1,
            Minimum = 1,
            Maximum = 3650,
            Width = 110
        };

        BuildLayout();
        LoadSettings(settings);
    }

    private void BuildLayout()
    {
        var buttonsPanel = BuildButtonsPanel();
        buttonsPanel.Dock = DockStyle.Bottom;
        Controls.Add(buttonsPanel);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ColumnCount = 1,
            RowCount = 4
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var introLabel = new Label
        {
            Text = "Configure where JPEG files are stored and how images are generated when you use C&C to JPEG.",
            AutoSize = true,
            MaximumSize = new Size(710, 0),
            Margin = new Padding(0, 0, 0, 12)
        };
        root.Controls.Add(introLabel, 0, 0);

        root.Controls.Add(BuildStorageGroup(), 0, 1);
        root.Controls.Add(BuildImageGroup(), 0, 2);
        root.Controls.Add(new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) }, 0, 3);
    }

    private GroupBox BuildStorageGroup()
    {
        var group = new GroupBox
        {
            Text = "Storage",
            Dock = DockStyle.Top,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 12),
            AutoSize = true
        };

        var table = CreateTableLayout();
        table.Controls.Add(CreateRowLabel("Temp folder"), 0, 0);
        table.Controls.Add(_tempFolderTextBox, 1, 0);
        table.Controls.Add(CreateSmallButton("Open folder", (_, _) => OpenTempFolder()), 2, 0);

        table.Controls.Add(_useCustomFolderCheckBox, 1, 1);
        table.SetColumnSpan(_useCustomFolderCheckBox, 2);

        table.Controls.Add(CreateRowLabel("Custom output folder"), 0, 2);
        table.Controls.Add(_customFolderTextBox, 1, 2);
        table.Controls.Add(_browseButton, 2, 2);

        table.Controls.Add(CreateRowLabel("Auto-clean temp files"), 0, 3);
        table.Controls.Add(WrapInline(_cleanupDaysNumeric, CreateInlineLabel("day(s)")), 1, 3);
        table.Controls.Add(CreateHintLabel("Used only when temp folder mode is active."), 2, 3);

        group.Controls.Add(table);
        return group;
    }

    private GroupBox BuildImageGroup()
    {
        var group = new GroupBox
        {
            Text = "Image settings",
            Dock = DockStyle.Top,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 12),
            AutoSize = true
        };

        var table = CreateTableLayout();
        table.Controls.Add(CreateRowLabel("Max file size"), 0, 0);
        table.Controls.Add(WrapInline(_maxFileSizeNumeric, CreateInlineLabel("MB")), 1, 0);
        table.Controls.Add(CreateHintLabel("Default is 9.8 MB."), 2, 0);

        table.Controls.Add(CreateRowLabel("Image quality"), 0, 1);
        table.Controls.Add(_qualitySlider, 1, 1);
        table.Controls.Add(_qualityValueLabel, 2, 1);

        table.Controls.Add(CreateRowLabel("Resolution"), 0, 2);
        table.Controls.Add(_keepOriginalResolutionCheckBox, 1, 2);
        table.Controls.Add(CreateHintLabel("Disable this to set a longest-side cap."), 2, 2);

        table.Controls.Add(new Panel(), 0, 3);
        table.Controls.Add(BuildResolutionInputs(), 1, 3);
        table.Controls.Add(CreateHintLabel("Aspect ratio is kept automatically."), 2, 3);

        group.Controls.Add(table);
        return group;
    }

    private FlowLayoutPanel BuildButtonsPanel()
    {
        var panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = false,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(14, 10, 14, 14),
            Height = 64
        };

        var saveButton = CreateSmallButton("Save");
        saveButton.Click += (_, _) => SaveSettings();

        var cancelButton = CreateSmallButton("Cancel");
        cancelButton.DialogResult = DialogResult.Cancel;

        var resetButton = CreateSmallButton("Reset to defaults");
        resetButton.AutoSize = true;
        resetButton.Click += (_, _) => LoadSettings(HeicToClipboardSettings.CreateDefault());

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        panel.Controls.Add(saveButton);
        panel.Controls.Add(cancelButton);
        panel.Controls.Add(resetButton);
        return panel;
    }

    private Control BuildResolutionInputs()
    {
        var panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0)
        };

        panel.Controls.Add(CreateInlineLabel("Longest side"));
        panel.Controls.Add(_maxLongestSideTextBox);
        panel.Controls.Add(CreateInlineLabel("px"));
        return panel;
    }

    private static TableLayoutPanel CreateTableLayout()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 0
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        return table;
    }

    private static TextBox CreateTextBox()
    {
        return new TextBox
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(0, 4, 10, 4)
        };
    }

    private static TextBox CreateReadOnlyTextBox()
    {
        return new TextBox
        {
            ReadOnly = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(0, 4, 10, 4)
        };
    }

    private static Button CreateSmallButton(string text, EventHandler? clickHandler = null)
    {
        var button = new Button
        {
            Text = text,
            Width = 110,
            Height = 30,
            Margin = new Padding(0, 4, 0, 4)
        };

        if (clickHandler is not null)
        {
            button.Click += clickHandler;
        }

        return button;
    }

    private static Label CreateRowLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 10, 8)
        };
    }

    private static Label CreateHintLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 0, 8),
            MaximumSize = new Size(160, 0)
        };
    }

    private static Label CreateInlineLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(8, 6, 0, 0)
        };
    }

    private static FlowLayoutPanel WrapInline(Control control, Control trailingLabel)
    {
        var panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 0)
        };
        panel.Controls.Add(control);
        panel.Controls.Add(trailingLabel);
        return panel;
    }

    private void LoadSettings(HeicToClipboardSettings settings)
    {
        var sanitized = settings.Sanitize();
        _tempFolderTextBox.Text = _tempDirectory;
        _useCustomFolderCheckBox.Checked = sanitized.UseCustomOutputFolder;
        _customFolderTextBox.Text = sanitized.CustomOutputFolder;
        _maxFileSizeNumeric.Value = sanitized.MaxFileSizeMb;
        _qualitySlider.Value = sanitized.InitialJpegQuality;
        _qualityValueLabel.Text = $"{sanitized.InitialJpegQuality}%";
        _keepOriginalResolutionCheckBox.Checked = sanitized.KeepOriginalResolution;
        _maxLongestSideTextBox.Text = sanitized.MaxLongestSidePx?.ToString() ?? string.Empty;
        _cleanupDaysNumeric.Value = sanitized.TempCleanupDays;
        UpdateCustomFolderState();
        UpdateResolutionState();
    }

    private void UpdateCustomFolderState()
    {
        var enabled = _useCustomFolderCheckBox.Checked;
        _customFolderTextBox.Enabled = enabled;
        _browseButton.Enabled = enabled;
    }

    private void UpdateResolutionState()
    {
        var enabled = !_keepOriginalResolutionCheckBox.Checked;
        _maxLongestSideTextBox.Enabled = enabled;
    }

    private void BrowseForFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose an output folder for generated JPEG files.",
            UseDescriptionForTitle = true,
            SelectedPath = _customFolderTextBox.Text
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _customFolderTextBox.Text = dialog.SelectedPath;
        }
    }

    private void OpenTempFolder()
    {
        try
        {
            Directory.CreateDirectory(_tempDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = _tempDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, AppConstants.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveSettings()
    {
        if (!TryBuildSettings(out var settings))
        {
            return;
        }

        try
        {
            _settingsStore.Save(settings);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, AppConstants.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool TryBuildSettings(out HeicToClipboardSettings settings)
    {
        settings = HeicToClipboardSettings.CreateDefault();

        string customFolder = string.Empty;
        if (_useCustomFolderCheckBox.Checked)
        {
            customFolder = (_customFolderTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(customFolder))
            {
                MessageBox.Show(this, "Choose a custom output folder or disable the custom output option.", AppConstants.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            try
            {
                Directory.CreateDirectory(customFolder);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, $"The custom output folder could not be created.{Environment.NewLine}{Environment.NewLine}{exception.Message}", AppConstants.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        int? maxLongestSide = null;
        if (!_keepOriginalResolutionCheckBox.Checked)
        {
            var maxLongestSideText = (_maxLongestSideTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(maxLongestSideText))
            {
                MessageBox.Show(this, "Enter a longest side value, or enable Keep original resolution.", AppConstants.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!int.TryParse(maxLongestSideText, out var parsedLongestSide) || parsedLongestSide <= 0)
            {
                MessageBox.Show(this, "Longest side must be a positive whole number.", AppConstants.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            maxLongestSide = parsedLongestSide;
        }

        settings = new HeicToClipboardSettings
        {
            UseCustomOutputFolder = _useCustomFolderCheckBox.Checked,
            CustomOutputFolder = customFolder,
            MaxFileSizeMb = _maxFileSizeNumeric.Value,
            InitialJpegQuality = _qualitySlider.Value,
            KeepOriginalResolution = _keepOriginalResolutionCheckBox.Checked,
            MaxLongestSidePx = maxLongestSide,
            TempCleanupDays = (int)_cleanupDaysNumeric.Value
        }.Sanitize();

        return true;
    }
}
