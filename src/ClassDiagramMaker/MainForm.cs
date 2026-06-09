using System.Diagnostics;
using ClassDiagramMaker.Analysis;

namespace ClassDiagramMaker;

public sealed class MainForm : Form
{
    private readonly ClassDiagramService _service;
    private readonly TextBox _projectFolderTextBox = new();
    private readonly TextBox _searchFolderTextBox = new();
    private readonly TextBox _searchFileTextBox = new();
    private readonly TextBox _outputPathTextBox = new();
    private readonly ComboBox _outputFormatComboBox = new();
    private readonly ComboBox _displayModeComboBox = new();
    private readonly CheckBox _includeInheritanceCheckBox = new();
    private readonly CheckBox _includeRealizationCheckBox = new();
    private readonly CheckBox _includeAssociationCheckBox = new();
    private readonly CheckBox _includeDependencyCheckBox = new();
    private readonly CheckBox _includeRelatedTypesCheckBox = new();
    private readonly NumericUpDown _relatedDepthNumericUpDown = new();
    private readonly CheckBox _unlimitedRelatedDepthCheckBox = new();
    private readonly CheckBox _showReferencedMembersOnlyCheckBox = new();
    private readonly CheckBox _splitOutputCheckBox = new();
    private readonly CheckBox _includeSplitIndexCheckBox = new();
    private readonly Button _generateButton = new();
    private readonly Button _cancelButton = new();
    private readonly ProgressBar _progressBar = new();
    private readonly Label _stageLabel = new();
    private readonly Label _messageLabel = new();
    private readonly Label _fileCountLabel = new();
    private readonly Label _outputLabel = new();
    private readonly Button _openOutputFolderButton = new();
    private CancellationTokenSource? _generationCancellation;
    private string? _lastOutputDirectory;

    public MainForm(ClassDiagramService service)
    {
        _service = service;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "ClassDiagramMaker";
        MinimumSize = new Size(980, 520);
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(14)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var inputPanel = BuildInputPanel();
        var optionsPanel = BuildOptionsPanel();
        var progressPanel = BuildProgressPanel();

        root.Controls.Add(inputPanel, 0, 0);
        root.Controls.Add(optionsPanel, 0, 1);
        root.Controls.Add(progressPanel, 0, 2);
        Controls.Add(root);

        _generateButton.Click += GenerateButton_Click;
        _cancelButton.Click += CancelButton_Click;
        _openOutputFolderButton.Click += OpenOutputFolderButton_Click;
    }

    private Control BuildInputPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 5,
            Padding = new Padding(0, 0, 0, 10)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));

        AddPathRow(panel, 0, "対象プロジェクト", _projectFolderTextBox, "参照...", BrowseProjectFile);
        AddPathRow(panel, 1, "検索対象フォルダ", _searchFolderTextBox, "参照...", BrowseSearchFolder);
        AddPathRow(panel, 2, "検索対象ファイル", _searchFileTextBox, "参照...", BrowseSearchFile);
        AddPathRow(panel, 3, "出力先", _outputPathTextBox, "参照...", BrowseOutputPath);

        var helpLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ForeColor = SystemColors.GrayText,
            Text = "対象プロジェクトは .csproj を選択します。検索対象ファイルが空の場合は再帰解析します。"
        };
        panel.Controls.Add(helpLabel, 1, 4);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true
        };

        _generateButton.Text = "生成";
        _generateButton.AutoSize = true;
        _generateButton.MinimumSize = new Size(96, 34);

        _cancelButton.Text = "キャンセル";
        _cancelButton.AutoSize = true;
        _cancelButton.MinimumSize = new Size(96, 34);
        _cancelButton.Enabled = false;

        buttonPanel.Controls.Add(_generateButton);
        buttonPanel.Controls.Add(_cancelButton);
        panel.Controls.Add(buttonPanel, 2, 4);

        return panel;
    }

    private Control BuildOptionsPanel()
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Text = "表示オプション",
            Padding = new Padding(10)
        };

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 6
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 6; row++)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        var outputFormatLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "出力形式"
        };

        _outputFormatComboBox.Dock = DockStyle.Left;
        _outputFormatComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _outputFormatComboBox.Width = 220;
        _outputFormatComboBox.Items.AddRange(new object[]
        {
            "Mermaid (.mmd)",
            "Excel (.xlsx)"
        });
        _outputFormatComboBox.SelectedIndex = 0;
        _outputFormatComboBox.SelectedIndexChanged += (_, _) => UpdateOutputFormatState();

        var displayLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "表示モード"
        };

        _displayModeComboBox.Dock = DockStyle.Left;
        _displayModeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _displayModeComboBox.Width = 220;
        _displayModeComboBox.Items.AddRange(new object[]
        {
            "型だけ",
            "主要メンバー",
            "全メンバー"
        });
        _displayModeComboBox.SelectedIndex = 2;

        var relationshipLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "関係"
        };

        var relationshipPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        ConfigureRelationshipCheckBox(_includeInheritanceCheckBox, "継承", checkedByDefault: true);
        ConfigureRelationshipCheckBox(_includeRealizationCheckBox, "interface 実装", checkedByDefault: true);
        ConfigureRelationshipCheckBox(_includeAssociationCheckBox, "フィールド/プロパティ関連", checkedByDefault: true);
        ConfigureRelationshipCheckBox(_includeDependencyCheckBox, "メソッド依存", checkedByDefault: true);

        relationshipPanel.Controls.Add(_includeInheritanceCheckBox);
        relationshipPanel.Controls.Add(_includeRealizationCheckBox);
        relationshipPanel.Controls.Add(_includeAssociationCheckBox);
        relationshipPanel.Controls.Add(_includeDependencyCheckBox);

        var relatedLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "検索ファイル"
        };

        var relatedPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        ConfigureRelationshipCheckBox(_includeRelatedTypesCheckBox, "関連型も表示", checkedByDefault: true);
        _includeRelatedTypesCheckBox.CheckedChanged += (_, _) => UpdateRelatedOptionState();

        var relatedDepthLabel = new Label
        {
            AutoSize = true,
            Text = "深さ",
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 8, 6, 4)
        };

        _relatedDepthNumericUpDown.Minimum = 0;
        _relatedDepthNumericUpDown.Maximum = 99;
        _relatedDepthNumericUpDown.Value = 1;
        _relatedDepthNumericUpDown.Width = 64;
        _relatedDepthNumericUpDown.Margin = new Padding(0, 4, 18, 4);

        ConfigureRelationshipCheckBox(_unlimitedRelatedDepthCheckBox, "無制限", checkedByDefault: false);
        _unlimitedRelatedDepthCheckBox.CheckedChanged += (_, _) => UpdateRelatedOptionState();

        ConfigureRelationshipCheckBox(_showReferencedMembersOnlyCheckBox, "関連型は使用メンバーのみ", checkedByDefault: false);

        relatedPanel.Controls.Add(_includeRelatedTypesCheckBox);
        relatedPanel.Controls.Add(relatedDepthLabel);
        relatedPanel.Controls.Add(_relatedDepthNumericUpDown);
        relatedPanel.Controls.Add(_unlimitedRelatedDepthCheckBox);
        relatedPanel.Controls.Add(_showReferencedMembersOnlyCheckBox);

        var splitLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "分割出力"
        };

        ConfigureRelationshipCheckBox(_splitOutputCheckBox, "分割して出力", checkedByDefault: false);
        _splitOutputCheckBox.CheckedChanged += (_, _) => UpdateSplitOptionState();

        var splitFileLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "分割補助"
        };

        var splitFilePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        ConfigureRelationshipCheckBox(_includeSplitIndexCheckBox, "index.md を出力", checkedByDefault: true);

        splitFilePanel.Controls.Add(_includeSplitIndexCheckBox);

        panel.Controls.Add(outputFormatLabel, 0, 0);
        panel.Controls.Add(_outputFormatComboBox, 1, 0);
        panel.Controls.Add(displayLabel, 0, 1);
        panel.Controls.Add(_displayModeComboBox, 1, 1);
        panel.Controls.Add(relationshipLabel, 0, 2);
        panel.Controls.Add(relationshipPanel, 1, 2);
        panel.Controls.Add(relatedLabel, 0, 3);
        panel.Controls.Add(relatedPanel, 1, 3);
        panel.Controls.Add(splitLabel, 0, 4);
        panel.Controls.Add(_splitOutputCheckBox, 1, 4);
        panel.Controls.Add(splitFileLabel, 0, 5);
        panel.Controls.Add(splitFilePanel, 1, 5);

        UpdateRelatedOptionState();
        UpdateSplitOptionState();
        group.Controls.Add(panel);
        return group;
    }

    private static void ConfigureRelationshipCheckBox(CheckBox checkBox, string text, bool checkedByDefault)
    {
        checkBox.Text = text;
        checkBox.Checked = checkedByDefault;
        checkBox.AutoSize = true;
        checkBox.Margin = new Padding(0, 4, 18, 4);
    }

    private static void AddPathRow(
        TableLayoutPanel panel,
        int row,
        string label,
        TextBox textBox,
        string buttonText,
        EventHandler browseHandler)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var labelControl = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = label
        };

        textBox.Dock = DockStyle.Fill;
        textBox.Margin = new Padding(0, 4, 8, 4);

        var button = new Button
        {
            Text = buttonText,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 4)
        };
        button.Click += browseHandler;

        panel.Controls.Add(labelControl, 0, row);
        panel.Controls.Add(textBox, 1, row);
        panel.Controls.Add(button, 2, row);
    }

    private Control BuildProgressPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(0, 0, 0, 10)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));

        _stageLabel.Text = "待機中";
        _stageLabel.Font = new Font(Font, FontStyle.Bold);
        _stageLabel.AutoSize = true;
        _stageLabel.Dock = DockStyle.Fill;

        _fileCountLabel.Text = "0 / 0 files";
        _fileCountLabel.AutoSize = true;
        _fileCountLabel.Dock = DockStyle.Fill;
        _fileCountLabel.TextAlign = ContentAlignment.MiddleRight;

        _messageLabel.Text = "入力して生成を開始してください。";
        _messageLabel.AutoSize = true;
        _messageLabel.Dock = DockStyle.Fill;
        _messageLabel.ForeColor = SystemColors.GrayText;

        _progressBar.Dock = DockStyle.Fill;
        _progressBar.Height = 18;
        _progressBar.Style = ProgressBarStyle.Continuous;

        _outputLabel.Text = "出力なし";
        _outputLabel.AutoSize = true;
        _outputLabel.Dock = DockStyle.Fill;
        _outputLabel.ForeColor = SystemColors.GrayText;

        _openOutputFolderButton.Text = "出力フォルダを開く";
        _openOutputFolderButton.AutoSize = true;
        _openOutputFolderButton.Dock = DockStyle.Right;
        _openOutputFolderButton.Enabled = false;
        _openOutputFolderButton.MinimumSize = new Size(150, 30);

        panel.Controls.Add(_stageLabel, 0, 0);
        panel.Controls.Add(_fileCountLabel, 1, 0);
        panel.Controls.Add(_messageLabel, 0, 1);
        panel.SetColumnSpan(_messageLabel, 2);
        panel.Controls.Add(_progressBar, 0, 2);
        panel.SetColumnSpan(_progressBar, 2);
        panel.Controls.Add(_outputLabel, 0, 3);
        panel.Controls.Add(_openOutputFolderButton, 1, 3);

        return panel;
    }

    private void BrowseProjectFile(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "対象プロジェクトファイルを選択",
            Filter = "C# project files (*.csproj)|*.csproj|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        var currentDirectory = GetProjectDirectory(_projectFolderTextBox.Text);
        if (!string.IsNullOrWhiteSpace(currentDirectory) && Directory.Exists(currentDirectory))
        {
            dialog.InitialDirectory = currentDirectory;
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _projectFolderTextBox.Text = dialog.FileName;
            if (string.IsNullOrWhiteSpace(_searchFolderTextBox.Text))
            {
                _searchFolderTextBox.Text = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
            }
        }
    }

    private void BrowseSearchFolder(object? sender, EventArgs e)
    {
        var currentValue = string.IsNullOrWhiteSpace(_searchFolderTextBox.Text)
            ? GetProjectDirectory(_projectFolderTextBox.Text) ?? string.Empty
            : _searchFolderTextBox.Text;
        if (TrySelectFolder("検索対象フォルダを選択", currentValue, out var folder))
        {
            _searchFolderTextBox.Text = folder;
        }
    }

    private void BrowseSearchFile(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "検索対象ファイルを選択",
            Filter = "Supported source files (*.cs;*.cshtml)|*.cs;*.cshtml|C# files (*.cs)|*.cs|Razor files (*.cshtml)|*.cshtml|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(_searchFolderTextBox.Text) && Directory.Exists(_searchFolderTextBox.Text))
        {
            dialog.InitialDirectory = _searchFolderTextBox.Text;
        }
        else
        {
            var projectDirectory = GetProjectDirectory(_projectFolderTextBox.Text);
            if (!string.IsNullOrWhiteSpace(projectDirectory) && Directory.Exists(projectDirectory))
            {
                dialog.InitialDirectory = projectDirectory;
            }
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _searchFileTextBox.Text = dialog.FileName;
        }
    }

    private void BrowseOutputPath(object? sender, EventArgs e)
    {
        var outputFormat = GetSelectedOutputFormat();
        using var dialog = new SaveFileDialog
        {
            Title = "出力先を選択",
            Filter = outputFormat == DiagramOutputFormat.Excel
                ? "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*"
                : "Mermaid files (*.mmd)|*.mmd|All files (*.*)|*.*",
            DefaultExt = outputFormat == DiagramOutputFormat.Excel ? "xlsx" : "mmd",
            OverwritePrompt = true
        };

        if (!string.IsNullOrWhiteSpace(_outputPathTextBox.Text))
        {
            var directory = Path.GetDirectoryName(_outputPathTextBox.Text);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                dialog.InitialDirectory = directory;
            }
            dialog.FileName = Path.GetFileName(_outputPathTextBox.Text);
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _outputPathTextBox.Text = dialog.FileName;
        }
    }

    private static bool TrySelectFolder(string description, string currentValue, out string folder)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = description,
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (!string.IsNullOrWhiteSpace(currentValue) && Directory.Exists(currentValue))
        {
            dialog.SelectedPath = currentValue;
        }

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            folder = dialog.SelectedPath;
            return true;
        }

        folder = string.Empty;
        return false;
    }

    private static string? GetProjectDirectory(string projectValue)
    {
        if (string.IsNullOrWhiteSpace(projectValue))
        {
            return null;
        }

        return File.Exists(projectValue)
            ? Path.GetDirectoryName(projectValue)
            : projectValue;
    }

    private async void GenerateButton_Click(object? sender, EventArgs e)
    {
        if (!TryCreateRequest(out var request))
        {
            return;
        }

        _generationCancellation = new CancellationTokenSource();
        SetRunning(true);
        ResetProgress();

        try
        {
            var progress = new Progress<GenerationProgress>(ReportProgress);
            var result = await Task.Run(
                () => _service.GenerateAsync(request, progress, _generationCancellation.Token));

            _outputLabel.Text = result.OutputPaths.Count > 1
                ? $"出力: {result.OutputPath} ({result.OutputPaths.Count} outputs)"
                : $"出力: {result.OutputPath}";
            _lastOutputDirectory = Path.GetDirectoryName(result.OutputPath);
            _openOutputFolderButton.Enabled = !string.IsNullOrWhiteSpace(_lastOutputDirectory) &&
                Directory.Exists(_lastOutputDirectory);
            _stageLabel.Text = "完了";
            _messageLabel.Text = $"生成完了: {result.TypeCount} types, {result.RelationshipCount} relationships";
        }
        catch (OperationCanceledException)
        {
            _stageLabel.Text = "キャンセル";
            _messageLabel.Text = "処理をキャンセルしました。";
        }
        catch (Exception ex)
        {
            _stageLabel.Text = "失敗";
            _messageLabel.Text = ex.Message;
            MessageBox.Show(this, ex.Message, "生成に失敗しました", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _generationCancellation.Dispose();
            _generationCancellation = null;
            SetRunning(false);
        }
    }

    private void CancelButton_Click(object? sender, EventArgs e)
    {
        _generationCancellation?.Cancel();
    }

    private void OpenOutputFolderButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastOutputDirectory) || !Directory.Exists(_lastOutputDirectory))
        {
            MessageBox.Show(this, "出力フォルダが見つかりません。", "出力フォルダを開けません", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _openOutputFolderButton.Enabled = false;
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _lastOutputDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "出力フォルダを開けません", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool TryCreateRequest(out GenerationRequest request)
    {
        var projectFile = _projectFolderTextBox.Text.Trim();
        var searchFolder = _searchFolderTextBox.Text.Trim();
        var searchFile = _searchFileTextBox.Text.Trim();
        var outputPath = _outputPathTextBox.Text.Trim();
        var outputFormat = GetSelectedOutputFormat();

        if (string.IsNullOrWhiteSpace(projectFile))
        {
            ShowValidationError("対象プロジェクトの .csproj を入力してください。");
            request = default!;
            return false;
        }

        if (!projectFile.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            ShowValidationError("対象プロジェクトには .csproj ファイルを指定してください。");
            request = default!;
            return false;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            ShowValidationError("出力先を入力してください。");
            request = default!;
            return false;
        }

        outputPath = EnsureOutputExtension(outputPath, outputFormat);
        _outputPathTextBox.Text = outputPath;

        request = new GenerationRequest(
            projectFile,
            searchFolder,
            string.IsNullOrWhiteSpace(searchFile) ? null : searchFile,
            outputPath)
        {
            Options = new DiagramGenerationOptions(
                DisplayMode: GetSelectedDisplayMode(),
                IncludeInheritance: _includeInheritanceCheckBox.Checked,
                IncludeRealization: _includeRealizationCheckBox.Checked,
                IncludeAssociation: _includeAssociationCheckBox.Checked,
                IncludeDependency: _includeDependencyCheckBox.Checked,
                OutputFormat: outputFormat)
            {
                SplitOutput = new DiagramSplitOptions(
                    Enabled: _splitOutputCheckBox.Checked,
                    IncludeOverview: false,
                    IncludeIndex: outputFormat == DiagramOutputFormat.Mermaid && _includeSplitIndexCheckBox.Checked),
                RelatedTypes = new RelatedTypeOptions(
                    Enabled: _includeRelatedTypesCheckBox.Checked,
                    Depth: (int)_relatedDepthNumericUpDown.Value,
                    Unlimited: _unlimitedRelatedDepthCheckBox.Checked,
                    ShowReferencedMembersOnly: _showReferencedMembersOnlyCheckBox.Checked)
            }
        };
        return true;
    }

    private DiagramOutputFormat GetSelectedOutputFormat()
    {
        return _outputFormatComboBox.SelectedIndex switch
        {
            1 => DiagramOutputFormat.Excel,
            _ => DiagramOutputFormat.Mermaid
        };
    }

    private DiagramDisplayMode GetSelectedDisplayMode()
    {
        return _displayModeComboBox.SelectedIndex switch
        {
            0 => DiagramDisplayMode.TypeOnly,
            1 => DiagramDisplayMode.KeyMembers,
            _ => DiagramDisplayMode.AllMembers
        };
    }

    private static string EnsureOutputExtension(string outputPath, DiagramOutputFormat outputFormat)
    {
        var expectedExtension = outputFormat == DiagramOutputFormat.Excel ? ".xlsx" : ".mmd";
        var currentExtension = Path.GetExtension(outputPath);
        if (string.IsNullOrWhiteSpace(currentExtension))
        {
            return $"{outputPath}{expectedExtension}";
        }

        return string.Equals(currentExtension, expectedExtension, StringComparison.OrdinalIgnoreCase)
            ? outputPath
            : Path.ChangeExtension(outputPath, expectedExtension);
    }

    private void UpdateSplitOptionState()
    {
        var enabled = _splitOutputCheckBox.Checked;
        _includeSplitIndexCheckBox.Enabled = enabled && GetSelectedOutputFormat() == DiagramOutputFormat.Mermaid;
    }

    private void UpdateRelatedOptionState()
    {
        var enabled = _includeRelatedTypesCheckBox.Checked;
        _unlimitedRelatedDepthCheckBox.Enabled = enabled;
        _relatedDepthNumericUpDown.Enabled = enabled && !_unlimitedRelatedDepthCheckBox.Checked;
        _showReferencedMembersOnlyCheckBox.Enabled = enabled;
    }

    private void UpdateOutputFormatState()
    {
        UpdateOutputPathExtension();
        UpdateSplitOptionState();
    }

    private void UpdateOutputPathExtension()
    {
        var outputPath = _outputPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        var updatedPath = EnsureOutputExtension(outputPath, GetSelectedOutputFormat());
        if (!string.Equals(outputPath, updatedPath, StringComparison.Ordinal))
        {
            _outputPathTextBox.Text = updatedPath;
        }
    }

    private void ShowValidationError(string message)
    {
        MessageBox.Show(this, message, "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void ReportProgress(GenerationProgress progress)
    {
        _stageLabel.Text = progress.Stage;
        _messageLabel.Text = progress.Message;
        _fileCountLabel.Text = $"{progress.ProcessedFiles} / {progress.TotalFiles} files";
        _progressBar.Value = Math.Clamp(progress.Percent, _progressBar.Minimum, _progressBar.Maximum);
    }

    private void ResetProgress()
    {
        _progressBar.Value = 0;
        _stageLabel.Text = "処理中";
        _messageLabel.Text = "開始しています...";
        _fileCountLabel.Text = "0 / 0 files";
        _outputLabel.Text = "出力なし";
        _lastOutputDirectory = null;
        _openOutputFolderButton.Enabled = false;
    }

    private void SetRunning(bool running)
    {
        _generateButton.Enabled = !running;
        _cancelButton.Enabled = running;
        _openOutputFolderButton.Enabled = !running &&
            !string.IsNullOrWhiteSpace(_lastOutputDirectory) &&
            Directory.Exists(_lastOutputDirectory);
        Cursor = running ? Cursors.WaitCursor : Cursors.Default;
    }
}
