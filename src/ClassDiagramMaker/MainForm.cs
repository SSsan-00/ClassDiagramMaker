using ClassDiagramMaker.Analysis;

namespace ClassDiagramMaker;

public sealed class MainForm : Form
{
    private const int PreferredLogPanelHeight = 190;
    private const int PreferredLogPanelMinHeight = 120;
    private const int PreferredMermaidPanelMinHeight = 220;

    private readonly ClassDiagramService _service;
    private readonly TextBox _projectFolderTextBox = new();
    private readonly TextBox _searchFolderTextBox = new();
    private readonly TextBox _searchFileTextBox = new();
    private readonly TextBox _outputPathTextBox = new();
    private readonly ComboBox _displayModeComboBox = new();
    private readonly CheckBox _includeInheritanceCheckBox = new();
    private readonly CheckBox _includeRealizationCheckBox = new();
    private readonly CheckBox _includeAssociationCheckBox = new();
    private readonly CheckBox _includeDependencyCheckBox = new();
    private readonly CheckBox _splitOutputCheckBox = new();
    private readonly ComboBox _splitModeComboBox = new();
    private readonly CheckBox _includeSplitOverviewCheckBox = new();
    private readonly CheckBox _includeSplitIndexCheckBox = new();
    private readonly Button _generateButton = new();
    private readonly Button _cancelButton = new();
    private readonly ProgressBar _progressBar = new();
    private readonly Label _stageLabel = new();
    private readonly Label _messageLabel = new();
    private readonly Label _fileCountLabel = new();
    private readonly Label _outputLabel = new();
    private readonly TextBox _logTextBox = new();
    private readonly TextBox _mermaidTextBox = new();
    private CancellationTokenSource? _generationCancellation;

    public MainForm(ClassDiagramService service)
    {
        _service = service;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "ClassDiagramMaker";
        MinimumSize = new Size(980, 720);
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(14)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var inputPanel = BuildInputPanel();
        var optionsPanel = BuildOptionsPanel();
        var progressPanel = BuildProgressPanel();
        var outputSplit = BuildOutputSplit();

        root.Controls.Add(inputPanel, 0, 0);
        root.Controls.Add(optionsPanel, 0, 1);
        root.Controls.Add(progressPanel, 0, 2);
        root.Controls.Add(outputSplit, 0, 3);
        Controls.Add(root);

        _generateButton.Click += GenerateButton_Click;
        _cancelButton.Click += CancelButton_Click;
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

        AddPathRow(panel, 0, "対象プロジェクト", _projectFolderTextBox, "参照...", BrowseProjectFolder);
        AddPathRow(panel, 1, "検索対象フォルダ", _searchFolderTextBox, "参照...", BrowseSearchFolder);
        AddPathRow(panel, 2, "検索対象ファイル", _searchFileTextBox, "参照...", BrowseSearchFile);
        AddPathRow(panel, 3, "出力先", _outputPathTextBox, "参照...", BrowseOutputPath);

        var helpLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ForeColor = SystemColors.GrayText,
            Text = "検索対象ファイルが空の場合は再帰解析します。Razor は .cshtml と .cshtml.cs をペアで解析します。"
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
            RowCount = 5
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 5; row++)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

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

        var splitLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "分割出力"
        };

        ConfigureRelationshipCheckBox(_splitOutputCheckBox, "分割して出力", checkedByDefault: false);
        _splitOutputCheckBox.CheckedChanged += (_, _) => UpdateSplitOptionState();

        var splitModeLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "分割単位"
        };

        _splitModeComboBox.Dock = DockStyle.Left;
        _splitModeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _splitModeComboBox.Width = 220;
        _splitModeComboBox.Items.AddRange(new object[]
        {
            "namespace",
            "フォルダ"
        });
        _splitModeComboBox.SelectedIndex = 0;

        var splitFileLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "分割ファイル"
        };

        var splitFilePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        ConfigureRelationshipCheckBox(_includeSplitOverviewCheckBox, "全体図も出力", checkedByDefault: true);
        ConfigureRelationshipCheckBox(_includeSplitIndexCheckBox, "index.md を出力", checkedByDefault: true);

        splitFilePanel.Controls.Add(_includeSplitOverviewCheckBox);
        splitFilePanel.Controls.Add(_includeSplitIndexCheckBox);

        panel.Controls.Add(displayLabel, 0, 0);
        panel.Controls.Add(_displayModeComboBox, 1, 0);
        panel.Controls.Add(relationshipLabel, 0, 1);
        panel.Controls.Add(relationshipPanel, 1, 1);
        panel.Controls.Add(splitLabel, 0, 2);
        panel.Controls.Add(_splitOutputCheckBox, 1, 2);
        panel.Controls.Add(splitModeLabel, 0, 3);
        panel.Controls.Add(_splitModeComboBox, 1, 3);
        panel.Controls.Add(splitFileLabel, 0, 4);
        panel.Controls.Add(splitFilePanel, 1, 4);

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

        panel.Controls.Add(_stageLabel, 0, 0);
        panel.Controls.Add(_fileCountLabel, 1, 0);
        panel.Controls.Add(_messageLabel, 0, 1);
        panel.SetColumnSpan(_messageLabel, 2);
        panel.Controls.Add(_progressBar, 0, 2);
        panel.SetColumnSpan(_progressBar, 2);
        panel.Controls.Add(_outputLabel, 0, 3);
        panel.SetColumnSpan(_outputLabel, 2);

        return panel;
    }

    private Control BuildOutputSplit()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal
        };
        var splitterInitialized = false;
        var configuringSplitter = false;

        split.HandleCreated += (_, _) =>
        {
            configuringSplitter = true;
            try
            {
                splitterInitialized = ConfigureOutputSplit(split, usePreferredDistance: !splitterInitialized) || splitterInitialized;
            }
            finally
            {
                configuringSplitter = false;
            }
        };
        split.SizeChanged += (_, _) =>
        {
            configuringSplitter = true;
            try
            {
                splitterInitialized = ConfigureOutputSplit(split, usePreferredDistance: !splitterInitialized) || splitterInitialized;
            }
            finally
            {
                configuringSplitter = false;
            }
        };
        split.SplitterMoved += (_, _) =>
        {
            if (!configuringSplitter)
            {
                splitterInitialized = true;
            }
        };

        split.Panel1.Controls.Add(BuildTextSection("ログ", _logTextBox, readOnly: true));
        split.Panel2.Controls.Add(BuildTextSection("Mermaid", _mermaidTextBox, readOnly: false));
        return split;
    }

    private static bool ConfigureOutputSplit(SplitContainer split, bool usePreferredDistance)
    {
        var availableHeight = split.Height - split.SplitterWidth;
        if (availableHeight <= 0)
        {
            return false;
        }

        var panel1MinSize = Math.Min(PreferredLogPanelMinHeight, Math.Max(0, availableHeight / 3));
        var panel2MinSize = Math.Min(PreferredMermaidPanelMinHeight, Math.Max(0, availableHeight - panel1MinSize));
        var minDistance = panel1MinSize;
        var maxDistance = availableHeight - panel2MinSize;
        if (maxDistance < minDistance)
        {
            panel2MinSize = Math.Max(0, availableHeight - panel1MinSize);
            maxDistance = availableHeight - panel2MinSize;
        }

        split.Panel1MinSize = 0;
        split.Panel2MinSize = 0;

        var preferredDistance = Math.Min(PreferredLogPanelHeight, availableHeight / 2);
        var distance = usePreferredDistance
            ? preferredDistance
            : split.SplitterDistance;
        split.SplitterDistance = Math.Clamp(distance, minDistance, maxDistance);
        split.Panel1MinSize = panel1MinSize;
        split.Panel2MinSize = panel2MinSize;
        return usePreferredDistance && preferredDistance >= minDistance && preferredDistance <= maxDistance;
    }

    private static Control BuildTextSection(string title, TextBox textBox, bool readOnly)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var label = new Label
        {
            Text = title,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
            Padding = new Padding(0, 0, 0, 4)
        };

        textBox.Dock = DockStyle.Fill;
        textBox.Multiline = true;
        textBox.ScrollBars = ScrollBars.Both;
        textBox.WordWrap = false;
        textBox.ReadOnly = readOnly;
        textBox.Font = new Font("Consolas", 10);

        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(textBox, 0, 1);
        return panel;
    }

    private void BrowseProjectFolder(object? sender, EventArgs e)
    {
        if (TrySelectFolder("対象プロジェクトフォルダを選択", _projectFolderTextBox.Text, out var folder))
        {
            _projectFolderTextBox.Text = folder;
            if (string.IsNullOrWhiteSpace(_searchFolderTextBox.Text))
            {
                _searchFolderTextBox.Text = folder;
            }
        }
    }

    private void BrowseSearchFolder(object? sender, EventArgs e)
    {
        if (TrySelectFolder("検索対象フォルダを選択", _searchFolderTextBox.Text, out var folder))
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

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _searchFileTextBox.Text = dialog.FileName;
        }
    }

    private void BrowseOutputPath(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Title = "出力先を選択",
            Filter = "Mermaid files (*.mmd)|*.mmd|Markdown files (*.md)|*.md|All files (*.*)|*.*",
            DefaultExt = "mmd",
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

    private async void GenerateButton_Click(object? sender, EventArgs e)
    {
        if (!TryCreateRequest(out var request))
        {
            return;
        }

        _generationCancellation = new CancellationTokenSource();
        SetRunning(true);
        ResetProgress();
        AppendLog("Generating Mermaid class diagram...");

        try
        {
            var progress = new Progress<GenerationProgress>(ReportProgress);
            var result = await Task.Run(
                () => _service.GenerateAsync(request, progress, _generationCancellation.Token));

            _mermaidTextBox.Text = result.Mermaid;
            _outputLabel.Text = result.OutputPaths.Count > 1
                ? $"出力: {result.OutputPath} ({result.OutputPaths.Count} files)"
                : $"出力: {result.OutputPath}";
            _stageLabel.Text = "完了";
            _messageLabel.Text = $"生成完了: {result.TypeCount} types, {result.RelationshipCount} relationships";
            AppendLog($"Wrote {result.OutputPath}");
            foreach (var outputPath in result.OutputPaths.Skip(1))
            {
                AppendLog($"Wrote {outputPath}");
            }
        }
        catch (OperationCanceledException)
        {
            _stageLabel.Text = "キャンセル";
            _messageLabel.Text = "処理をキャンセルしました。";
            AppendLog("Canceled.");
        }
        catch (Exception ex)
        {
            _stageLabel.Text = "失敗";
            _messageLabel.Text = ex.Message;
            AppendLog(ex.ToString());
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

    private bool TryCreateRequest(out GenerationRequest request)
    {
        var projectFolder = _projectFolderTextBox.Text.Trim();
        var searchFolder = _searchFolderTextBox.Text.Trim();
        var searchFile = _searchFileTextBox.Text.Trim();
        var outputPath = _outputPathTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            ShowValidationError("対象プロジェクトフォルダを入力してください。");
            request = default!;
            return false;
        }

        if (string.IsNullOrWhiteSpace(searchFile) && string.IsNullOrWhiteSpace(searchFolder))
        {
            ShowValidationError("検索対象ファイルが空の場合は、検索対象フォルダを入力してください。");
            request = default!;
            return false;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            ShowValidationError("出力先を入力してください。");
            request = default!;
            return false;
        }

        request = new GenerationRequest(
            projectFolder,
            searchFolder,
            string.IsNullOrWhiteSpace(searchFile) ? null : searchFile,
            outputPath)
        {
            Options = new DiagramGenerationOptions(
                DisplayMode: GetSelectedDisplayMode(),
                IncludeInheritance: _includeInheritanceCheckBox.Checked,
                IncludeRealization: _includeRealizationCheckBox.Checked,
                IncludeAssociation: _includeAssociationCheckBox.Checked,
                IncludeDependency: _includeDependencyCheckBox.Checked)
            {
                SplitOutput = new DiagramSplitOptions(
                    Enabled: _splitOutputCheckBox.Checked,
                    Mode: GetSelectedSplitMode(),
                    IncludeOverview: _includeSplitOverviewCheckBox.Checked,
                    IncludeIndex: _includeSplitIndexCheckBox.Checked)
            }
        };
        return true;
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

    private DiagramSplitMode GetSelectedSplitMode()
    {
        return _splitModeComboBox.SelectedIndex switch
        {
            1 => DiagramSplitMode.Folder,
            _ => DiagramSplitMode.Namespace
        };
    }

    private void UpdateSplitOptionState()
    {
        var enabled = _splitOutputCheckBox.Checked;
        _splitModeComboBox.Enabled = enabled;
        _includeSplitOverviewCheckBox.Enabled = enabled;
        _includeSplitIndexCheckBox.Enabled = enabled;
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
        AppendLog(progress.Message);
    }

    private void ResetProgress()
    {
        _progressBar.Value = 0;
        _stageLabel.Text = "処理中";
        _messageLabel.Text = "開始しています...";
        _fileCountLabel.Text = "0 / 0 files";
        _outputLabel.Text = "出力なし";
        _logTextBox.Clear();
        _mermaidTextBox.Clear();
    }

    private void SetRunning(bool running)
    {
        _generateButton.Enabled = !running;
        _cancelButton.Enabled = running;
        Cursor = running ? Cursors.WaitCursor : Cursors.Default;
    }

    private void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }
}
