using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using MarkdownPad.Core;
using MarkdownPad.Core.Images;
using MarkdownPad.Core.Markdown;
using MarkdownPad.Core.Settings;
using MarkdownPad.Core.Text;
using MarkdownPad.Infrastructure;
using MarkdownPad.Services;

namespace MarkdownPad.ViewModels;

/// <summary>
/// Application state and commands. Extracted from the window's code-behind,
/// which previously held all 870 lines of file I/O, rendering, clipboard and
/// caret logic in a single class that doubled as its own DataContext.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private const string MarkdownFilter =
        "Markdownファイル (*.md;*.markdown)|*.md;*.markdown|テキストファイル (*.txt)|*.txt|すべてのファイル (*.*)|*.*";

    private const string ImageFilter =
        "画像ファイル (*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|すべてのファイル (*.*)|*.*";

    private readonly IDialogService _dialogs;
    private readonly MarkdownRenderer _renderer = new();
    private readonly ImageInsertionService _images = new();
    private readonly RecoveryStore _recovery = new(AppPaths.Backups);
    private readonly LineIndex _lineIndex = new();
    private readonly SearchSession _search = new();
    private readonly DispatcherTimer _previewTimer;
    private readonly DispatcherTimer _autoSaveTimer;

    private PreviewHost? _preview;
    private TextBoxEditor? _editor;

    private string _markdownText = string.Empty;
    private string _currentFilePath = string.Empty;
    private TextDocumentFormat _format = TextDocumentFormat.Default;
    private bool _isModified;
    private bool _suppressModified;
    private string _statusMessage = "準備完了";
    private int _currentLine = 1;
    private int _currentColumn = 1;
    private string _title = "MarkdownPad - 無題";
    private string? _previewError;
    private int _selectionLength;
    private FileStamp? _diskStamp;
    private bool _reloadPromptOpen;

    public MainViewModel(IDialogService dialogs, AppSettings settings)
    {
        _dialogs = dialogs;
        Settings = settings;

        _previewTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };
        _previewTimer.Tick += (_, _) =>
        {
            _previewTimer.Stop();
            UpdatePreview();
        };

        _autoSaveTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(settings.AutoSaveIntervalSeconds),
        };
        _autoSaveTimer.Tick += (_, _) => WriteRecoverySnapshot();
        if (settings.AutoSaveEnabled) _autoSaveTimer.Start();

        InitializeCommands();

        foreach (var option in TextEncodings.Options)
        {
            EncodingOptions.Add(new MenuCommandItem(option.DisplayName, SetEncodingCommand, option.DisplayName));
        }

        SyncRecentFiles();
    }

    public AppSettings Settings { get; }

    /// <summary>Recently opened files, newest first, each carrying its own command.</summary>
    public ObservableCollection<MenuCommandItem> RecentFiles { get; } = new();

    /// <summary>Encodings offered by the 書式 &gt; 文字コード menu.</summary>
    public ObservableCollection<MenuCommandItem> EncodingOptions { get; } = new();

    /// <summary>
    /// The three theme flags behave like radio buttons. Unchecking the active
    /// one is rejected, and the notification pushes the checkmark back so the
    /// menu cannot drift out of step with the setting.
    /// </summary>
    public bool IsLightTheme
    {
        get => PreviewTheme == PreviewTheme.Light;
        set => SelectTheme(PreviewTheme.Light, value);
    }

    public bool IsDarkTheme
    {
        get => PreviewTheme == PreviewTheme.Dark;
        set => SelectTheme(PreviewTheme.Dark, value);
    }

    public bool IsSystemTheme
    {
        get => PreviewTheme == PreviewTheme.FollowSystem;
        set => SelectTheme(PreviewTheme.FollowSystem, value);
    }

    private void SelectTheme(PreviewTheme theme, bool selected)
    {
        if (selected) PreviewTheme = theme;
        else NotifyThemeFlags();
    }

    private void NotifyThemeFlags()
    {
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(IsSystemTheme));
    }

    #region Document state

    public string MarkdownText
    {
        get => _markdownText;
        set
        {
            if (!SetProperty(ref _markdownText, value)) return;

            _lineIndex.Rebuild(value);

            if (!_suppressModified) IsModified = true;

            OnPropertyChanged(nameof(CharacterCount));
            OnPropertyChanged(nameof(WordCount));
            OnPropertyChanged(nameof(LineCount));

            SchedulePreviewUpdate();
        }
    }

    public string CurrentFilePath
    {
        get => _currentFilePath;
        private set
        {
            if (!SetProperty(ref _currentFilePath, value)) return;

            UpdateTitle();
            _preview?.SetDocumentDirectory(DocumentDirectory);
        }
    }

    public bool IsModified
    {
        get => _isModified;
        private set
        {
            if (SetProperty(ref _isModified, value)) UpdateTitle();
        }
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public TextDocumentFormat DocumentFormat
    {
        get => _format;
        private set
        {
            if (!SetProperty(ref _format, value)) return;
            OnPropertyChanged(nameof(EncodingStatus));
        }
    }

    public string EncodingStatus => DocumentFormat.StatusText;

    public string DocumentName =>
        string.IsNullOrEmpty(CurrentFilePath) ? "無題" : Path.GetFileName(CurrentFilePath);

    public string? DocumentDirectory =>
        string.IsNullOrEmpty(CurrentFilePath) ? null : Path.GetDirectoryName(CurrentFilePath);

    public int CharacterCount => _markdownText.Length;

    public int LineCount => _lineIndex.LineCount;

    /// <summary>Whitespace-separated runs; a rough but useful writing metric.</summary>
    public int WordCount
    {
        get
        {
            int words = 0;
            bool inWord = false;

            foreach (char c in _markdownText)
            {
                if (char.IsWhiteSpace(c)) { inWord = false; }
                else if (!inWord) { inWord = true; words++; }
            }

            return words;
        }
    }

    public int CurrentLine
    {
        get => _currentLine;
        private set => SetProperty(ref _currentLine, value);
    }

    public int CurrentColumn
    {
        get => _currentColumn;
        private set => SetProperty(ref _currentColumn, value);
    }

    /// <summary>Characters currently selected; editors show this next to the caret position.</summary>
    public int SelectionLength
    {
        get => _selectionLength;
        private set
        {
            if (!SetProperty(ref _selectionLength, value)) return;
            OnPropertyChanged(nameof(SelectionText));
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    public bool HasSelection => _selectionLength > 0;

    public string SelectionText => $"（{_selectionLength} 文字選択）";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string? PreviewError
    {
        get => _previewError;
        private set
        {
            if (!SetProperty(ref _previewError, value)) return;
            OnPropertyChanged(nameof(HasPreviewError));
        }
    }

    public bool HasPreviewError => !string.IsNullOrEmpty(PreviewError);

    #endregion

    #region View options

    public bool IsPreviewVisible
    {
        get => Settings.PreviewVisible;
        set
        {
            if (Settings.PreviewVisible == value) return;

            Settings.PreviewVisible = value;
            OnPropertyChanged();

            if (value) UpdatePreview();
        }
    }

    public bool WordWrap
    {
        get => Settings.WordWrap;
        set
        {
            if (Settings.WordWrap == value) return;
            Settings.WordWrap = value;
            OnPropertyChanged();
        }
    }

    public bool ShowLineNumbers
    {
        get => Settings.ShowLineNumbers;
        set
        {
            if (Settings.ShowLineNumbers == value) return;
            Settings.ShowLineNumbers = value;
            OnPropertyChanged();
        }
    }

    public bool SyncScroll
    {
        get => Settings.SyncScroll;
        set
        {
            if (Settings.SyncScroll == value) return;
            Settings.SyncScroll = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Copy dropped or pasted image files into the document's folder.</summary>
    public bool CopyImportedImages
    {
        get => Settings.CopyImportedImages;
        set
        {
            if (Settings.CopyImportedImages == value) return;
            Settings.CopyImportedImages = value;
            OnPropertyChanged();
        }
    }

    public bool AllowRawHtml
    {
        get => Settings.AllowRawHtml;
        set
        {
            if (Settings.AllowRawHtml == value) return;

            Settings.AllowRawHtml = value;
            OnPropertyChanged();
            UpdatePreview();
        }
    }

    public double EditorFontSize
    {
        get => Settings.EditorFontSize;
        set
        {
            double clamped = Math.Clamp(value, 8, 48);
            if (Math.Abs(Settings.EditorFontSize - clamped) < 0.01) return;

            Settings.EditorFontSize = clamped;
            OnPropertyChanged();
        }
    }

    public PreviewTheme PreviewTheme
    {
        get => Settings.PreviewTheme;
        set
        {
            if (Settings.PreviewTheme == value) return;

            Settings.PreviewTheme = value;
            OnPropertyChanged();
            NotifyThemeFlags();

            _preview?.SetTheme(value);
        }
    }

    #endregion

    #region Commands

    public RelayCommand NewCommand { get; private set; } = null!;
    public RelayCommand OpenCommand { get; private set; } = null!;
    public RelayCommand<string> OpenRecentCommand { get; private set; } = null!;
    public RelayCommand SaveCommand { get; private set; } = null!;
    public RelayCommand SaveAsCommand { get; private set; } = null!;
    public RelayCommand ExportHtmlCommand { get; private set; } = null!;
    public RelayCommand ExportPdfCommand { get; private set; } = null!;
    public RelayCommand PrintCommand { get; private set; } = null!;
    public RelayCommand ExitCommand { get; private set; } = null!;
    public RelayCommand FindCommand { get; private set; } = null!;
    public RelayCommand ReplaceCommand { get; private set; } = null!;
    public RelayCommand FindNextCommand { get; private set; } = null!;
    public RelayCommand FindPreviousCommand { get; private set; } = null!;
    public RelayCommand DuplicateLineCommand { get; private set; } = null!;
    public RelayCommand DeleteLineCommand { get; private set; } = null!;
    public RelayCommand MoveLineUpCommand { get; private set; } = null!;
    public RelayCommand MoveLineDownCommand { get; private set; } = null!;
    public RelayCommand InsertImageCommand { get; private set; } = null!;
    public RelayCommand InsertLinkCommand { get; private set; } = null!;
    public RelayCommand InsertTableCommand { get; private set; } = null!;
    public RelayCommand InsertHorizontalRuleCommand { get; private set; } = null!;
    public RelayCommand<string> HeadingCommand { get; private set; } = null!;
    public RelayCommand BoldCommand { get; private set; } = null!;
    public RelayCommand ItalicCommand { get; private set; } = null!;
    public RelayCommand StrikethroughCommand { get; private set; } = null!;
    public RelayCommand InlineCodeCommand { get; private set; } = null!;
    public RelayCommand CodeBlockCommand { get; private set; } = null!;
    public RelayCommand QuoteCommand { get; private set; } = null!;
    public RelayCommand BulletListCommand { get; private set; } = null!;
    public RelayCommand OrderedListCommand { get; private set; } = null!;
    public RelayCommand TaskListCommand { get; private set; } = null!;
    public RelayCommand<string> SetEncodingCommand { get; private set; } = null!;
    public RelayCommand<string> SetNewLineCommand { get; private set; } = null!;
    public RelayCommand ZoomInCommand { get; private set; } = null!;
    public RelayCommand ZoomOutCommand { get; private set; } = null!;
    public RelayCommand ZoomResetCommand { get; private set; } = null!;
    public RelayCommand MarkdownGuideCommand { get; private set; } = null!;
    public RelayCommand AboutCommand { get; private set; } = null!;

    /// <summary>Raised when the user picks File &gt; 終了.</summary>
    public event EventHandler? ExitRequested;

    private void InitializeCommands()
    {
        NewCommand = new RelayCommand(NewDocument);
        OpenCommand = new RelayCommand(OpenDocument);
        OpenRecentCommand = new RelayCommand<string>(OpenRecent);
        SaveCommand = new RelayCommand(() => Save());
        SaveAsCommand = new RelayCommand(() => SaveAs());
        ExportHtmlCommand = new RelayCommand(ExportHtml);
        ExportPdfCommand = new RelayCommand(ExportPdf);
        PrintCommand = new RelayCommand(Print);
        ExitCommand = new RelayCommand(() => ExitRequested?.Invoke(this, EventArgs.Empty));

        FindCommand = new RelayCommand(() => ShowFind(replace: false));
        ReplaceCommand = new RelayCommand(() => ShowFind(replace: true));
        FindNextCommand = new RelayCommand(() => RepeatSearch(forward: true));
        FindPreviousCommand = new RelayCommand(() => RepeatSearch(forward: false));
        DuplicateLineCommand = new RelayCommand(DuplicateSelectedLines);
        DeleteLineCommand = new RelayCommand(DeleteSelectedLines);
        MoveLineUpCommand = new RelayCommand(() => MoveSelectedLines(-1));
        MoveLineDownCommand = new RelayCommand(() => MoveSelectedLines(1));

        InsertImageCommand = new RelayCommand(InsertImageFromFile);
        InsertLinkCommand = new RelayCommand(InsertLink);
        InsertTableCommand = new RelayCommand(() => InsertSnippet(
            "\n| 列1 | 列2 |\n|-----|-----|\n|     |     |\n"));
        InsertHorizontalRuleCommand = new RelayCommand(() => InsertSnippet("\n---\n"));

        HeadingCommand = new RelayCommand<string>(level => ApplyLinePrefix(new string('#', ToLevel(level)) + " "));
        BoldCommand = new RelayCommand(() => ApplyWrap("**"));
        ItalicCommand = new RelayCommand(() => ApplyWrap("*"));
        StrikethroughCommand = new RelayCommand(() => ApplyWrap("~~"));
        InlineCodeCommand = new RelayCommand(() => ApplyWrap("`"));
        CodeBlockCommand = new RelayCommand(ApplyCodeBlock);
        QuoteCommand = new RelayCommand(() => ApplyLinePrefix("> "));
        BulletListCommand = new RelayCommand(() => ApplyLinePrefix("- "));
        TaskListCommand = new RelayCommand(() => ApplyLinePrefix("- [ ] "));
        OrderedListCommand = new RelayCommand(ApplyOrderedList);

        SetEncodingCommand = new RelayCommand<string>(ChangeEncoding);
        SetNewLineCommand = new RelayCommand<string>(ChangeNewLine);

        ZoomInCommand = new RelayCommand(() => EditorFontSize += 1);
        ZoomOutCommand = new RelayCommand(() => EditorFontSize -= 1);
        ZoomResetCommand = new RelayCommand(() => EditorFontSize = 14);

        MarkdownGuideCommand = new RelayCommand(ShowMarkdownGuide);
        AboutCommand = new RelayCommand(ShowAbout);
    }

    private static int ToLevel(string? level) =>
        int.TryParse(level, out int parsed) ? Math.Clamp(parsed, 1, 6) : 1;

    #endregion

    #region Wiring

    public void AttachEditor(TextBoxEditor editor) => _editor = editor;

    public void AttachPreview(PreviewHost preview)
    {
        _preview = preview;
        preview.InitializationFailed += (_, message) => PreviewError = message;
        preview.SetDocumentDirectory(DocumentDirectory);
        preview.SetTheme(PreviewTheme);
    }

    /// <summary>Called by the view whenever the caret or the selection moves.</summary>
    public void UpdateCaretPosition(int offset, int selectionLength = 0)
    {
        var (line, column) = _lineIndex.GetLineColumn(offset);
        CurrentLine = line;
        CurrentColumn = column;
        SelectionLength = selectionLength;
    }

    /// <summary>Called by the view when the editor scrolls, to follow along in the preview.</summary>
    public void SyncPreviewToEditorLine(int firstVisibleLineOffset)
    {
        if (!SyncScroll || !IsPreviewVisible) return;

        _preview?.ScrollToLine(_lineIndex.GetLineColumn(firstVisibleLineOffset).Line);
    }

    #endregion

    #region File operations

    /// <summary>
    /// Offers to open a snapshot left behind by a previous run. Called once at
    /// start-up, before any document is loaded.
    /// </summary>
    public void OfferRecovery()
    {
        var snapshot = _recovery.Read();
        if (snapshot is null) return;

        if (_dialogs.AskToRecover(snapshot))
        {
            SetDocument(snapshot.Text, snapshot.OriginalPath ?? string.Empty, TextDocumentFormat.Default);
            IsModified = true;
            StatusMessage = "前回の未保存データを復元しました";
        }
        else
        {
            _recovery.Clear();
        }
    }

    /// <summary>
    /// Called when the window is activated: notices that another program
    /// rewrote the open file and offers to reload it.
    /// </summary>
    /// <remarks>
    /// Comparing a stamp on activation is deliberately simpler than a file
    /// watcher: it cannot fire on the editor's own saves, needs no background
    /// thread, and only ever interrupts the user when they are actually here.
    /// </remarks>
    public void CheckForExternalChange()
    {
        if (_reloadPromptOpen || string.IsNullOrEmpty(CurrentFilePath) || _diskStamp is null) return;

        var current = TextFileService.TryReadStamp(CurrentFilePath);
        if (current is null || current == _diskStamp) return;

        _reloadPromptOpen = true;
        try
        {
            if (_dialogs.AskToReload(DocumentName, IsModified))
            {
                LoadFile(CurrentFilePath);
            }
            else
            {
                // Accept the new state as the baseline so the prompt does not
                // reappear on every activation.
                _diskStamp = current;
                IsModified = true;
            }
        }
        finally
        {
            _reloadPromptOpen = false;
        }
    }

    public bool ConfirmDiscardChanges()
    {
        if (!IsModified) return true;

        return _dialogs.AskToSaveChanges(DocumentName) switch
        {
            SaveChangesAnswer.Save => Save(),
            SaveChangesAnswer.Discard => true,
            _ => false,
        };
    }

    private void NewDocument()
    {
        if (!ConfirmDiscardChanges()) return;

        SetDocument(string.Empty, string.Empty, TextDocumentFormat.Default);
        _diskStamp = null;
        _recovery.Clear();
        StatusMessage = "新規ファイルを作成しました";
    }

    private void OpenDocument()
    {
        if (!ConfirmDiscardChanges()) return;

        string? path = _dialogs.PickFileToOpen(MarkdownFilter, "ファイルを開く", DocumentDirectory);
        if (path is not null) LoadFile(path);
    }

    private void OpenRecent(string path)
    {
        if (!ConfirmDiscardChanges()) return;
        LoadFile(path);
    }

    public void LoadFile(string path)
    {
        try
        {
            var loaded = TextFileService.Load(path);

            SetDocument(loaded.Text, path, loaded.Format);
            _diskStamp = TextFileService.TryReadStamp(path);
            _recovery.Clear();
            AddRecentFile(path);

            StatusMessage = $"ファイルを開きました: {Path.GetFileName(path)} ({loaded.Format.StatusText})";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            _dialogs.ShowError($"ファイルを開けませんでした: {e.Message}");
            RemoveRecentFile(path);
        }
    }

    public bool Save()
        => string.IsNullOrEmpty(CurrentFilePath) ? SaveAs() : WriteFile(CurrentFilePath);

    public bool SaveAs()
    {
        string? path = _dialogs.PickFileToSave(
            "Markdownファイル (*.md)|*.md|テキストファイル (*.txt)|*.txt|すべてのファイル (*.*)|*.*",
            "名前を付けて保存",
            ".md",
            string.IsNullOrEmpty(CurrentFilePath) ? "無題.md" : Path.GetFileName(CurrentFilePath),
            DocumentDirectory);

        if (path is null) return false;

        string? previousDirectory = DocumentDirectory;
        if (!WriteFile(path)) return false;

        CurrentFilePath = path;
        AddRecentFile(path);

        // References created while the document was unsaved point at the image
        // library; re-render so they resolve against the new location.
        if (!string.Equals(previousDirectory, DocumentDirectory, StringComparison.OrdinalIgnoreCase))
        {
            UpdatePreview();
        }

        return true;
    }

    private bool WriteFile(string path)
    {
        if (TextFileService.TryFindUnencodableText(MarkdownText, DocumentFormat, out string lost))
        {
            bool proceed = _dialogs.Confirm(
                $"この文書には {DocumentFormat.EncodingDisplayName} で表現できない文字 '{lost}' が含まれています。\n" +
                "このまま保存すると、その文字は '?' に置き換えられます。\n\n" +
                "続行するには [OK]、エンコーディングを変更するには [キャンセル] を選択してください。",
                "文字化けの警告");

            if (!proceed) return false;
        }

        try
        {
            TextFileService.Save(path, MarkdownText, DocumentFormat);

            _diskStamp = TextFileService.TryReadStamp(path);
            IsModified = false;
            _recovery.Clear();
            StatusMessage = $"ファイルを保存しました: {Path.GetFileName(path)}";
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            _dialogs.ShowError($"ファイルを保存できませんでした: {e.Message}");
            return false;
        }
    }

    private void ExportHtml()
    {
        string? path = _dialogs.PickFileToSave(
            "HTMLファイル (*.html)|*.html|すべてのファイル (*.*)|*.*",
            "HTMLとしてエクスポート",
            ".html",
            Path.GetFileNameWithoutExtension(DocumentName) + ".html",
            DocumentDirectory);

        if (path is null) return;

        try
        {
            // Exported HTML keeps the original relative links: the virtual-host
            // rewriting only makes sense inside the preview.
            string body = _renderer.Render(MarkdownText, new MarkdownRenderOptions { AllowRawHtml = AllowRawHtml });
            string document = HtmlExport.BuildStandaloneDocument(DocumentName, body);

            File.WriteAllText(path, document, TextEncodings.Utf8NoBom);
            StatusMessage = $"HTMLを書き出しました: {Path.GetFileName(path)}";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError($"HTMLを書き出せませんでした: {e.Message}");
        }
    }

    private async void ExportPdf()
    {
        if (_preview is null || !_preview.IsReady)
        {
            _dialogs.ShowError("プレビューが利用できないため、PDF を書き出せません。");
            return;
        }

        string? path = _dialogs.PickFileToSave(
            "PDFファイル (*.pdf)|*.pdf|すべてのファイル (*.*)|*.*",
            "PDFとしてエクスポート",
            ".pdf",
            Path.GetFileNameWithoutExtension(DocumentName) + ".pdf",
            DocumentDirectory);

        if (path is null) return;

        try
        {
            StatusMessage = "PDF を作成しています...";
            bool ok = await _preview.PrintToPdfAsync(path);

            StatusMessage = ok
                ? $"PDFを書き出しました: {Path.GetFileName(path)}"
                : "PDF の書き出しに失敗しました";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _dialogs.ShowError($"PDFを書き出せませんでした: {e.Message}");
        }
    }

    private void Print()
    {
        if (_preview is null || !_preview.IsReady)
        {
            _dialogs.ShowError("プレビューが利用できないため、印刷できません。");
            return;
        }

        _preview.ShowPrintDialog();
    }

    private void SetDocument(string text, string path, TextDocumentFormat format)
    {
        _suppressModified = true;
        try
        {
            MarkdownText = text;
            CurrentFilePath = path;
            DocumentFormat = format;
            IsModified = false;
        }
        finally
        {
            _suppressModified = false;
        }

        UpdatePreview();
    }

    private void AddRecentFile(string path)
    {
        Settings.AddRecentFile(path);
        SyncRecentFiles();
    }

    private void RemoveRecentFile(string path)
    {
        Settings.RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        SyncRecentFiles();
    }

    private void SyncRecentFiles()
    {
        RecentFiles.Clear();
        foreach (string file in Settings.RecentFiles)
        {
            RecentFiles.Add(new MenuCommandItem(file, OpenRecentCommand, file));
        }
    }

    #endregion

    #region Encoding

    private void ChangeEncoding(string displayName)
    {
        var option = TextEncodings.Options.FirstOrDefault(o => o.DisplayName == displayName);
        if (option is null) return;

        DocumentFormat = DocumentFormat with
        {
            Encoding = option.Encoding,
            HasBom = option.UseBom,
            EncodingDisplayName = option.DisplayName,
        };

        IsModified = true;
        StatusMessage = $"エンコーディングを {option.DisplayName} に変更しました（保存時に適用されます）";
    }

    private void ChangeNewLine(string styleName)
    {
        if (!Enum.TryParse(styleName, ignoreCase: true, out NewLineStyle style)) return;

        DocumentFormat = DocumentFormat with { NewLine = style };
        IsModified = true;
        StatusMessage = $"改行コードを {style.ToDisplayName()} に変更しました（保存時に適用されます）";
    }

    #endregion

    #region Editing

    private void ApplyWrap(string wrapper)
    {
        if (_editor is null) return;
        _editor.Apply(MarkdownFormatter.ToggleWrap(_editor.Text, _editor.SelectionStart, _editor.SelectionLength, wrapper));
    }

    private void ApplyLinePrefix(string prefix)
    {
        if (_editor is null) return;
        _editor.Apply(MarkdownFormatter.ToggleLinePrefix(_editor.Text, _editor.SelectionStart, _editor.SelectionLength, prefix));
    }

    private void ApplyOrderedList()
    {
        if (_editor is null) return;
        _editor.Apply(MarkdownFormatter.ToggleOrderedList(_editor.Text, _editor.SelectionStart, _editor.SelectionLength));
    }

    private void ApplyCodeBlock()
    {
        if (_editor is null) return;
        _editor.Apply(MarkdownFormatter.InsertCodeBlock(_editor.Text, _editor.SelectionStart, _editor.SelectionLength));
    }

    private void InsertSnippet(string snippet) => _editor?.InsertAtCaret(snippet);

    /// <summary>
    /// Enter: carries a list bullet, number, quote or indentation onto the next
    /// line, and clears the marker on an empty item. Returns false to let the
    /// editor insert an ordinary newline.
    /// </summary>
    public bool TryContinueLine()
    {
        // With a selection, Enter replaces it: the editor's own handling is right.
        if (_editor is null || _editor.SelectionLength > 0) return false;

        // The TextBox itself inserts CRLF, so matching it keeps the buffer uniform.
        var operation = EditorCommands.ContinueLine(_editor.Text, _editor.CaretIndex, "\r\n");
        if (operation is null) return false;

        _editor.Apply(operation);
        return true;
    }

    /// <summary>Tab / Shift+Tab over the selected lines.</summary>
    public void ChangeIndent(bool increase)
    {
        if (_editor is null) return;

        _editor.Apply(increase
            ? EditorCommands.Indent(_editor.Text, _editor.SelectionStart, _editor.SelectionLength)
            : EditorCommands.Outdent(_editor.Text, _editor.SelectionStart, _editor.SelectionLength));
    }

    /// <summary>Alt+Up / Alt+Down: reorders the selected lines.</summary>
    public void MoveSelectedLines(int direction)
    {
        if (_editor is null) return;

        var operation = EditorCommands.MoveLines(_editor.Text, _editor.SelectionStart, _editor.SelectionLength, direction);
        if (operation is not null) _editor.Apply(operation);
    }

    public void DuplicateSelectedLines()
    {
        if (_editor is null) return;
        _editor.Apply(EditorCommands.DuplicateLines(_editor.Text, _editor.SelectionStart, _editor.SelectionLength));
    }

    public void DeleteSelectedLines()
    {
        if (_editor is null) return;
        _editor.Apply(EditorCommands.DeleteLines(_editor.Text, _editor.SelectionStart, _editor.SelectionLength));
    }

    public void ZoomBy(int steps) => EditorFontSize += steps;

    private void ShowFind(bool replace)
    {
        if (_editor is null) return;
        _dialogs.ShowFindReplace(_editor, _search, replace, _editor.SelectedText);
    }

    /// <summary>
    /// F3 / Shift+F3: repeats the last search without the dialog, which is what
    /// every editor does. With nothing searched yet, it opens the dialog.
    /// </summary>
    private void RepeatSearch(bool forward)
    {
        if (_editor is null) return;

        if (!_search.HasQuery)
        {
            ShowFind(replace: false);
            return;
        }

        var query = _search.Query!;
        string text = _editor.Text;
        int start = forward ? _editor.SelectionStart + _editor.SelectionLength : _editor.SelectionStart;

        var match = forward
            ? TextSearch.FindNext(text, query, start, _search.Wrap)
            : TextSearch.FindPrevious(text, query, start, _search.Wrap);

        if (match is null)
        {
            StatusMessage = $"'{query.Pattern}' は見つかりませんでした";
            return;
        }

        _editor.Select(match.Value.Index, match.Value.Length);
        _editor.ScrollToOffset(match.Value.Index);
        StatusMessage = $"'{query.Pattern}' を {_lineIndex.GetLineColumn(match.Value.Index).Line} 行目で見つけました";
    }

    private void InsertLink()
    {
        if (_editor is null) return;

        var result = _dialogs.ShowInsertLink(_editor.SelectedText);
        if (!result.Confirmed) return;

        string label = string.IsNullOrEmpty(_editor.SelectedText) ? result.Text : _editor.SelectedText;
        if (string.IsNullOrEmpty(label)) label = result.Url;

        _editor.Apply(MarkdownFormatter.InsertText(
            _editor.Text, _editor.SelectionStart, _editor.SelectionLength,
            MarkdownFormatter.BuildLink(label, result.Url)));
    }

    private void InsertImageFromFile()
    {
        string? path = _dialogs.PickFileToOpen(ImageFilter, "画像を選択", DocumentDirectory);
        if (path is not null) InsertImageReference(ImportImageIfNeeded(path));
    }

    /// <summary>
    /// Copies an image into the document's own <c>images</c> folder so the note
    /// keeps working after the original is moved or the Downloads folder is
    /// emptied. Files already inside the document tree are linked in place.
    /// </summary>
    private string ImportImageIfNeeded(string imagePath)
    {
        if (!Settings.CopyImportedImages || string.IsNullOrEmpty(DocumentDirectory)) return imagePath;

        try
        {
            if (IsInsideDocumentTree(imagePath)) return imagePath;

            string target = _images.CopyIntoLibrary(imagePath, CurrentFilePath);
            StatusMessage = $"画像を取り込みました: {Path.GetFileName(target)}";
            return target;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            _dialogs.ShowError($"画像を取り込めませんでした: {e.Message}");
            return imagePath;
        }
    }

    private bool IsInsideDocumentTree(string path)
    {
        if (string.IsNullOrEmpty(DocumentDirectory)) return false;

        string relative = Path.GetRelativePath(DocumentDirectory, path);
        return !Path.IsPathRooted(relative) && !relative.StartsWith("..", StringComparison.Ordinal);
    }

    /// <summary>Inserts a Markdown reference to an image that is already on disk.</summary>
    public void InsertImageReference(string imagePath)
    {
        if (_editor is null) return;

        string markdownPath = _images.BuildMarkdownPath(imagePath, CurrentFilePath);
        string markdown = MarkdownFormatter.BuildLink(Path.GetFileNameWithoutExtension(imagePath), markdownPath, isImage: true);

        _editor.InsertAtCaret(markdown);
    }

    /// <summary>
    /// Saves a bitmap from the clipboard and links it. Returns false when nothing
    /// was inserted, so the caller can fall back to a normal paste.
    /// </summary>
    public bool PasteImage(System.Windows.Media.Imaging.BitmapSource image)
    {
        if (_editor is null) return false;

        try
        {
            var saved = _images.SaveClipboardImage(image, CurrentFilePath);

            _editor.InsertAtCaret(MarkdownFormatter.BuildLink(
                Path.GetFileNameWithoutExtension(saved.FileName), saved.MarkdownPath, isImage: true));

            StatusMessage = $"画像を保存しました: {saved.FileName}";
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            _dialogs.ShowError($"画像の貼り付けに失敗しました: {e.Message}");
            return false;
        }
    }

    /// <summary>Handles files dropped on the editor: images are linked, text files opened.</summary>
    public void HandleDroppedFiles(IReadOnlyList<string> paths)
    {
        var imagePaths = paths.Where(ImageFiles.IsImageFile).ToList();

        foreach (string image in imagePaths)
        {
            InsertImageReference(ImportImageIfNeeded(image));
        }

        if (imagePaths.Count > 0) return;

        string? document = paths.FirstOrDefault();
        if (document is null || !File.Exists(document)) return;

        if (!ConfirmDiscardChanges()) return;
        LoadFile(document);
    }

    private void ShowMarkdownGuide()
    {
        if (_dialogs.ShowMarkdownGuide())
        {
            InsertSnippet(MarkdownGuide.Text);
            StatusMessage = "マークダウンガイドを挿入しました";
        }
    }

    private void ShowAbout()
    {
        string version = typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

        _dialogs.ShowInformation(
            $"MarkdownPad v{version}\n\n" +
            "マークダウン対応メモ帳アプリケーション\n\n" +
            "・リアルタイムプレビュー（スクロール位置を保持）\n" +
            "・スクリーンショット / 画像の貼り付け\n" +
            "・文字コードと改行コードの自動判別と保持\n" +
            "・HTML / PDF エクスポート",
            "バージョン情報");
    }

    #endregion

    #region Preview

    private void SchedulePreviewUpdate()
    {
        if (!IsPreviewVisible) return;

        // Restarting the timer coalesces bursts of typing into one render.
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void UpdatePreview()
    {
        if (_preview is null || !IsPreviewVisible) return;

        try
        {
            string html = _renderer.Render(MarkdownText, new MarkdownRenderOptions
            {
                DocumentDirectory = DocumentDirectory ?? AppPaths.DefaultDocumentFolder,
                ResourceMapper = _preview.ResourceMapper,
                AllowRawHtml = AllowRawHtml,
            });

            _preview.Render(html);
            PreviewError = null;
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or IOException)
        {
            PreviewError = $"プレビューを生成できませんでした: {e.Message}";
        }
    }

    #endregion

    #region Shutdown

    private void WriteRecoverySnapshot()
    {
        if (IsModified) _recovery.Write(CurrentFilePath, MarkdownText);
    }

    /// <summary>Flushes preferences and clears the recovery snapshot on a clean exit.</summary>
    public void Shutdown()
    {
        _previewTimer.Stop();
        _autoSaveTimer.Stop();

        Settings.RecentFiles = RecentFiles.Select(item => (string)item.Parameter!).ToList();
        Settings.Save(AppPaths.SettingsFile);

        _recovery.Clear();
    }

    private void UpdateTitle()
        => Title = $"MarkdownPad - {DocumentName}{(IsModified ? " *" : string.Empty)}";

    #endregion
}
