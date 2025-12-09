using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Markdig;
using Microsoft.Win32;

namespace MarkdownPad;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    private string _markdownText = string.Empty;
    private string _currentFilePath = string.Empty;
    private bool _isModified = false;
    private bool _isPreviewVisible = true;
    private bool _wordWrap = true;
    private bool _showLineNumbers = true;
    private int _currentLine = 1;
    private int _currentColumn = 1;
    private string _statusMessage = "準備完了";
    private string _encodingName = "UTF-8";
    private readonly MarkdownPipeline _markdownPipeline;
    private System.Timers.Timer? _previewUpdateTimer;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        // Configure Markdig pipeline with extensions
        _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseAutoLinks()
            .UseTaskLists()
            .UseEmphasisExtras()
            .UsePipeTables()
            .UseGridTables()
            .Build();

        // Setup preview update timer (debounce)
        _previewUpdateTimer = new System.Timers.Timer(300);
        _previewUpdateTimer.Elapsed += (s, e) =>
        {
            _previewUpdateTimer?.Stop();
            Dispatcher.Invoke(UpdatePreview);
        };

        // Initialize commands
        InitializeCommands();

        // Initialize WebView2
        InitializeWebView();
    }

    #region Properties

    public string MarkdownText
    {
        get => _markdownText;
        set
        {
            if (_markdownText != value)
            {
                _markdownText = value;
                OnPropertyChanged();
                IsModified = true;
                UpdateCharacterCount();
            }
        }
    }

    public string CurrentFilePath
    {
        get => _currentFilePath;
        set
        {
            _currentFilePath = value;
            OnPropertyChanged();
            UpdateTitle();
        }
    }

    public bool IsModified
    {
        get => _isModified;
        set
        {
            _isModified = value;
            OnPropertyChanged();
            UpdateTitle();
        }
    }

    public bool IsPreviewVisible
    {
        get => _isPreviewVisible;
        set
        {
            _isPreviewVisible = value;
            OnPropertyChanged();
            if (value)
            {
                PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
                UpdatePreview();
            }
            else
            {
                PreviewColumn.Width = new GridLength(0);
            }
        }
    }

    public bool WordWrap
    {
        get => _wordWrap;
        set
        {
            _wordWrap = value;
            OnPropertyChanged();
        }
    }

    public bool ShowLineNumbers
    {
        get => _showLineNumbers;
        set
        {
            _showLineNumbers = value;
            OnPropertyChanged();
        }
    }

    public int CurrentLine
    {
        get => _currentLine;
        set
        {
            _currentLine = value;
            OnPropertyChanged();
        }
    }

    public int CurrentColumn
    {
        get => _currentColumn;
        set
        {
            _currentColumn = value;
            OnPropertyChanged();
        }
    }

    public int CharacterCount => _markdownText.Length;

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public string EncodingName
    {
        get => _encodingName;
        set
        {
            _encodingName = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Commands

    public ICommand NewCommand { get; private set; } = null!;
    public ICommand OpenCommand { get; private set; } = null!;
    public ICommand SaveCommand { get; private set; } = null!;
    public ICommand SaveAsCommand { get; private set; } = null!;
    public ICommand ExitCommand { get; private set; } = null!;
    public ICommand UndoCommand { get; private set; } = null!;
    public ICommand RedoCommand { get; private set; } = null!;
    public ICommand CutCommand { get; private set; } = null!;
    public ICommand CopyCommand { get; private set; } = null!;
    public ICommand PasteCommand { get; private set; } = null!;
    public ICommand SelectAllCommand { get; private set; } = null!;
    public ICommand FindCommand { get; private set; } = null!;
    public ICommand ReplaceCommand { get; private set; } = null!;
    public ICommand InsertImageCommand { get; private set; } = null!;
    public ICommand InsertLinkCommand { get; private set; } = null!;
    public ICommand InsertHeading1Command { get; private set; } = null!;
    public ICommand InsertHeading2Command { get; private set; } = null!;
    public ICommand InsertHeading3Command { get; private set; } = null!;
    public ICommand InsertBoldCommand { get; private set; } = null!;
    public ICommand InsertItalicCommand { get; private set; } = null!;
    public ICommand InsertCodeBlockCommand { get; private set; } = null!;
    public ICommand InsertBulletListCommand { get; private set; } = null!;
    public ICommand InsertNumberedListCommand { get; private set; } = null!;
    public ICommand MarkdownGuideCommand { get; private set; } = null!;
    public ICommand AboutCommand { get; private set; } = null!;

    private void InitializeCommands()
    {
        NewCommand = new RelayCommand(ExecuteNew);
        OpenCommand = new RelayCommand(ExecuteOpen);
        SaveCommand = new RelayCommand(ExecuteSave);
        SaveAsCommand = new RelayCommand(ExecuteSaveAs);
        ExitCommand = new RelayCommand(ExecuteExit);
        UndoCommand = new RelayCommand(ExecuteUndo, CanExecuteUndo);
        RedoCommand = new RelayCommand(ExecuteRedo, CanExecuteRedo);
        CutCommand = new RelayCommand(ExecuteCut, CanExecuteCut);
        CopyCommand = new RelayCommand(ExecuteCopy, CanExecuteCopy);
        PasteCommand = new RelayCommand(ExecutePaste, CanExecutePaste);
        SelectAllCommand = new RelayCommand(ExecuteSelectAll);
        FindCommand = new RelayCommand(ExecuteFind);
        ReplaceCommand = new RelayCommand(ExecuteReplace);
        InsertImageCommand = new RelayCommand(ExecuteInsertImage);
        InsertLinkCommand = new RelayCommand(ExecuteInsertLink);
        InsertHeading1Command = new RelayCommand(() => InsertMarkdown("# ", ""));
        InsertHeading2Command = new RelayCommand(() => InsertMarkdown("## ", ""));
        InsertHeading3Command = new RelayCommand(() => InsertMarkdown("### ", ""));
        InsertBoldCommand = new RelayCommand(() => InsertMarkdownWrap("**"));
        InsertItalicCommand = new RelayCommand(() => InsertMarkdownWrap("*"));
        InsertCodeBlockCommand = new RelayCommand(() => InsertMarkdown("```\n", "\n```"));
        InsertBulletListCommand = new RelayCommand(() => InsertMarkdown("- ", ""));
        InsertNumberedListCommand = new RelayCommand(() => InsertMarkdown("1. ", ""));
        MarkdownGuideCommand = new RelayCommand(ExecuteMarkdownGuide);
        AboutCommand = new RelayCommand(ExecuteAbout);
    }

    #endregion

    #region Command Implementations

    private void ExecuteNew()
    {
        if (!PromptSaveChanges()) return;

        MarkdownText = string.Empty;
        CurrentFilePath = string.Empty;
        IsModified = false;
        EditorTextBox.Clear();
        StatusMessage = "新規ファイルを作成しました";
    }

    private void ExecuteOpen()
    {
        if (!PromptSaveChanges()) return;

        var dialog = new OpenFileDialog
        {
            Filter = "Markdownファイル (*.md;*.markdown)|*.md;*.markdown|テキストファイル (*.txt)|*.txt|すべてのファイル (*.*)|*.*",
            Title = "ファイルを開く"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                MarkdownText = File.ReadAllText(dialog.FileName);
                CurrentFilePath = dialog.FileName;
                IsModified = false;
                StatusMessage = $"ファイルを開きました: {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ファイルを開けませんでした: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ExecuteSave()
    {
        if (string.IsNullOrEmpty(CurrentFilePath))
        {
            ExecuteSaveAs();
        }
        else
        {
            SaveFile(CurrentFilePath);
        }
    }

    private void ExecuteSaveAs()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Markdownファイル (*.md)|*.md|テキストファイル (*.txt)|*.txt|すべてのファイル (*.*)|*.*",
            Title = "名前を付けて保存",
            DefaultExt = ".md"
        };

        if (dialog.ShowDialog() == true)
        {
            SaveFile(dialog.FileName);
            CurrentFilePath = dialog.FileName;
        }
    }

    private void SaveFile(string path)
    {
        try
        {
            File.WriteAllText(path, MarkdownText);
            IsModified = false;
            StatusMessage = $"ファイルを保存しました: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ファイルを保存できませんでした: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExecuteExit()
    {
        Close();
    }

    private void ExecuteUndo()
    {
        EditorTextBox.Undo();
    }

    private bool CanExecuteUndo() => EditorTextBox?.CanUndo ?? false;

    private void ExecuteRedo()
    {
        EditorTextBox.Redo();
    }

    private bool CanExecuteRedo() => EditorTextBox?.CanRedo ?? false;

    private void ExecuteCut()
    {
        EditorTextBox.Cut();
    }

    private bool CanExecuteCut() => EditorTextBox?.SelectionLength > 0;

    private void ExecuteCopy()
    {
        EditorTextBox.Copy();
    }

    private bool CanExecuteCopy() => EditorTextBox?.SelectionLength > 0;

    private void ExecutePaste()
    {
        // Check for image in clipboard
        if (Clipboard.ContainsImage())
        {
            PasteImage();
        }
        else if (Clipboard.ContainsFileDropList())
        {
            var files = Clipboard.GetFileDropList();
            foreach (string? file in files)
            {
                if (file != null && IsImageFile(file))
                {
                    InsertImageReference(file);
                }
            }
        }
        else
        {
            EditorTextBox.Paste();
        }
    }

    private bool CanExecutePaste() => Clipboard.ContainsText() || Clipboard.ContainsImage() || Clipboard.ContainsFileDropList();

    private void ExecuteSelectAll()
    {
        EditorTextBox.SelectAll();
    }

    private void ExecuteFind()
    {
        var dialog = new FindReplaceDialog(EditorTextBox, false);
        dialog.Owner = this;
        dialog.Show();
    }

    private void ExecuteReplace()
    {
        var dialog = new FindReplaceDialog(EditorTextBox, true);
        dialog.Owner = this;
        dialog.Show();
    }

    private void ExecuteInsertImage()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "画像ファイル (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|すべてのファイル (*.*)|*.*",
            Title = "画像を選択"
        };

        if (dialog.ShowDialog() == true)
        {
            InsertImageReference(dialog.FileName);
        }
    }

    private void ExecuteInsertLink()
    {
        var dialog = new InsertLinkDialog();
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            string linkText = string.IsNullOrEmpty(EditorTextBox.SelectedText) ? dialog.LinkText : EditorTextBox.SelectedText;
            string markdown = $"[{linkText}]({dialog.LinkUrl})";
            InsertAtCursor(markdown);
        }
    }

    private void ExecuteMarkdownGuide()
    {
        var guide = @"# マークダウンガイド

## 基本的な書式

### 見出し
# 見出し1
## 見出し2
### 見出し3

### テキスト装飾
**太字** または __太字__
*斜体* または _斜体_
~~取り消し線~~

### リスト
箇条書き:
- 項目1
- 項目2
  - サブ項目

番号付きリスト:
1. 項目1
2. 項目2

### リンクと画像
[リンクテキスト](URL)
![画像の説明](画像のパス)

### コード
インラインコード: `code`

コードブロック:
```言語名
コード
```

### 引用
> 引用文

### 水平線
---

### テーブル
| 列1 | 列2 |
|-----|-----|
| A   | B   |
";
        MarkdownText = guide;
        IsModified = true;
        StatusMessage = "マークダウンガイドを表示しました";
    }

    private void ExecuteAbout()
    {
        MessageBox.Show(
            "MarkdownPad v1.0\n\nマークダウン対応メモ帳アプリケーション\n\n機能:\n・マークダウン記法のリアルタイムプレビュー\n・スクリーンショット・画像の貼り付け\n・基本的なテキスト編集機能",
            "バージョン情報",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    #endregion

    #region Helper Methods

    private async void InitializeWebView()
    {
        try
        {
            await PreviewWebView.EnsureCoreWebView2Async(null);
            UpdatePreview();
        }
        catch (Exception ex)
        {
            StatusMessage = $"プレビュー初期化エラー: {ex.Message}";
        }
    }

    private void UpdatePreview()
    {
        if (PreviewWebView.CoreWebView2 == null) return;

        try
        {
            string html = Markdig.Markdown.ToHtml(MarkdownText, _markdownPipeline);
            string fullHtml = WrapHtmlWithStyle(html);
            PreviewWebView.NavigateToString(fullHtml);
        }
        catch (Exception ex)
        {
            StatusMessage = $"プレビューエラー: {ex.Message}";
        }
    }

    private static string WrapHtmlWithStyle(string bodyContent)
    {
        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <meta name=""color-scheme"" content=""light"">
    <style>
        :root {{
            color-scheme: light;
        }}
        body {{
            font-family: 'Segoe UI', 'Meiryo', sans-serif;
            line-height: 1.6;
            padding: 20px;
            max-width: 100%;
            color: #333;
            background-color: #ffffff;
        }}
        h1, h2, h3, h4, h5, h6 {{
            margin-top: 24px;
            margin-bottom: 16px;
            font-weight: 600;
            line-height: 1.25;
            border-bottom: 1px solid #eaecef;
            padding-bottom: 0.3em;
        }}
        h1 {{ font-size: 2em; }}
        h2 {{ font-size: 1.5em; }}
        h3 {{ font-size: 1.25em; }}
        code {{
            background-color: #f6f8fa;
            padding: 0.2em 0.4em;
            border-radius: 3px;
            font-family: 'Consolas', 'Monaco', monospace;
            font-size: 85%;
        }}
        pre {{
            background-color: #f6f8fa;
            padding: 16px;
            overflow: auto;
            border-radius: 6px;
        }}
        pre code {{
            background: none;
            padding: 0;
        }}
        blockquote {{
            margin: 0;
            padding: 0 1em;
            color: #6a737d;
            border-left: 0.25em solid #dfe2e5;
        }}
        table {{
            border-collapse: collapse;
            width: 100%;
            margin: 16px 0;
        }}
        th, td {{
            border: 1px solid #dfe2e5;
            padding: 6px 13px;
        }}
        th {{
            background-color: #f6f8fa;
            font-weight: 600;
        }}
        tr:nth-child(even) {{
            background-color: #f6f8fa;
        }}
        img {{
            max-width: 100%;
            height: auto;
        }}
        a {{
            color: #0366d6;
            text-decoration: none;
        }}
        a:hover {{
            text-decoration: underline;
        }}
        hr {{
            border: 0;
            height: 1px;
            background: #e1e4e8;
            margin: 24px 0;
        }}
        ul, ol {{
            padding-left: 2em;
        }}
        li {{
            margin: 0.25em 0;
        }}
        .task-list-item {{
            list-style: none;
        }}
        .task-list-item input {{
            margin-right: 0.5em;
        }}
    </style>
</head>
<body>
{bodyContent}
</body>
</html>";
    }

    private void InsertMarkdown(string prefix, string suffix)
    {
        int selectionStart = EditorTextBox.SelectionStart;
        string selectedText = EditorTextBox.SelectedText;
        string newText = prefix + selectedText + suffix;

        EditorTextBox.SelectedText = newText;
        EditorTextBox.SelectionStart = selectionStart + prefix.Length;
        EditorTextBox.SelectionLength = selectedText.Length;
        EditorTextBox.Focus();
    }

    private void InsertMarkdownWrap(string wrapper)
    {
        InsertMarkdown(wrapper, wrapper);
    }

    private void InsertAtCursor(string text)
    {
        int selectionStart = EditorTextBox.SelectionStart;
        EditorTextBox.SelectedText = text;
        EditorTextBox.SelectionStart = selectionStart + text.Length;
        EditorTextBox.Focus();
    }

    private void PasteImage()
    {
        try
        {
            var image = Clipboard.GetImage();
            if (image == null) return;

            // Create images directory if needed
            string imagesDir = GetImagesDirectory();
            Directory.CreateDirectory(imagesDir);

            // Generate unique filename
            string fileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string filePath = Path.Combine(imagesDir, fileName);

            // Save image
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
                encoder.Save(fileStream);
            }

            // Insert markdown reference
            InsertImageReference(filePath);
            StatusMessage = $"画像を保存しました: {fileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"画像の貼り付けに失敗しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string GetImagesDirectory()
    {
        if (!string.IsNullOrEmpty(CurrentFilePath))
        {
            string? dir = Path.GetDirectoryName(CurrentFilePath);
            if (dir != null)
            {
                return Path.Combine(dir, "images");
            }
        }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MarkdownPad", "images");
    }

    private void InsertImageReference(string imagePath)
    {
        string relativePath = imagePath;

        // Try to make path relative if we have a current file
        if (!string.IsNullOrEmpty(CurrentFilePath))
        {
            string? currentDir = Path.GetDirectoryName(CurrentFilePath);
            if (currentDir != null)
            {
                try
                {
                    Uri imageUri = new Uri(imagePath);
                    Uri currentDirUri = new Uri(currentDir + Path.DirectorySeparatorChar);
                    relativePath = Uri.UnescapeDataString(currentDirUri.MakeRelativeUri(imageUri).ToString());
                }
                catch
                {
                    // Keep absolute path if relative path calculation fails
                }
            }
        }

        string fileName = Path.GetFileName(imagePath);
        string markdown = $"![{fileName}]({relativePath.Replace('\\', '/')})";
        InsertAtCursor(markdown);
    }

    private static bool IsImageFile(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp";
    }

    private void UpdateTitle()
    {
        string fileName = string.IsNullOrEmpty(CurrentFilePath) ? "無題" : Path.GetFileName(CurrentFilePath);
        string modified = IsModified ? "*" : "";
        Title = $"MarkdownPad - {fileName}{modified}";
    }

    private void UpdateCharacterCount()
    {
        OnPropertyChanged(nameof(CharacterCount));
    }

    private bool PromptSaveChanges()
    {
        if (!IsModified) return true;

        var result = MessageBox.Show(
            "変更を保存しますか?",
            "MarkdownPad",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        switch (result)
        {
            case MessageBoxResult.Yes:
                ExecuteSave();
                return !IsModified; // Return true only if save was successful
            case MessageBoxResult.No:
                return true;
            default:
                return false;
        }
    }

    #endregion

    #region Event Handlers

    private void EditorTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Debounce preview updates
        _previewUpdateTimer?.Stop();
        _previewUpdateTimer?.Start();

        // Update cursor position
        UpdateCursorPosition();
    }

    private void EditorTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Handle Ctrl+B for bold
        if (e.Key == Key.B && Keyboard.Modifiers == ModifierKeys.Control)
        {
            InsertMarkdownWrap("**");
            e.Handled = true;
        }
        // Handle Ctrl+I for italic
        else if (e.Key == Key.I && Keyboard.Modifiers == ModifierKeys.Control)
        {
            InsertMarkdownWrap("*");
            e.Handled = true;
        }
    }

    private void UpdateCursorPosition()
    {
        int caretIndex = EditorTextBox.CaretIndex;
        string text = EditorTextBox.Text;

        int line = 1;
        int column = 1;

        for (int i = 0; i < caretIndex && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        CurrentLine = line;
        CurrentColumn = column;
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (!PromptSaveChanges())
        {
            e.Cancel = true;
        }
        else
        {
            _previewUpdateTimer?.Dispose();
        }
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}

/// <summary>
/// Simple relay command implementation
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();
}