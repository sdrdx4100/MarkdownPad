using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using MarkdownPad.Core.Images;
using MarkdownPad.Core.Settings;
using MarkdownPad.Services;
using MarkdownPad.ViewModels;

namespace MarkdownPad.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly AppSettings _settings;
    private readonly PreviewHost _preview;
    private readonly DialogService _dialogs;

    public MainWindow(AppSettings settings)
    {
        InitializeComponent();

        _settings = settings;
        _dialogs = new DialogService(this);
        _viewModel = new MainViewModel(_dialogs, settings);
        DataContext = _viewModel;

        Width = settings.WindowWidth;
        Height = settings.WindowHeight;
        if (settings.WindowMaximized) WindowState = WindowState.Maximized;

        _preview = new PreviewHost(PreviewWebView);

        _viewModel.AttachEditor(new TextBoxEditor(EditorTextBox));
        _viewModel.AttachPreview(_preview);
        _viewModel.ExitRequested += (_, _) => Close();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Intercepting the paste at the data-object level catches Ctrl+V, the
        // context menu and Shift+Insert alike. A window-level KeyBinding could
        // not: the TextBox's own Paste command handles the gesture first.
        DataObject.AddPastingHandler(EditorTextBox, OnEditorPasting);

        EditorTextBox.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnEditorScrollChanged));

        Loaded += OnLoaded;
    }

    /// <summary>File passed on the command line, opened once the window is up.</summary>
    public string? StartupFilePath { get; init; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyPreviewLayout();
        EditorTextBox.Focus();

        await _preview.InitializeAsync();

        if (!string.IsNullOrEmpty(StartupFilePath) && File.Exists(StartupFilePath))
        {
            _viewModel.LoadFile(StartupFilePath);
        }
        else
        {
            _viewModel.OfferRecovery();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsPreviewVisible))
        {
            ApplyPreviewLayout();
        }
    }

    /// <summary>
    /// Collapses or restores the preview column.
    /// </summary>
    /// <remarks>
    /// Setting the width to zero was not enough on its own: the column also
    /// carried <c>MinWidth="200"</c> in XAML, so hiding the preview left a
    /// 200px empty strip. The minimum is now managed here alongside the width.
    /// </remarks>
    private void ApplyPreviewLayout()
    {
        if (_viewModel.IsPreviewVisible)
        {
            double ratio = Math.Clamp(_settings.EditorRatio, 0.1, 0.9);

            EditorColumn.Width = new GridLength(ratio, GridUnitType.Star);
            SplitterColumn.Width = GridLength.Auto;
            PreviewColumn.MinWidth = 200;
            PreviewColumn.Width = new GridLength(1 - ratio, GridUnitType.Star);
        }
        else
        {
            CaptureEditorRatio();

            EditorColumn.Width = new GridLength(1, GridUnitType.Star);
            SplitterColumn.Width = new GridLength(0);
            PreviewColumn.MinWidth = 0;
            PreviewColumn.Width = new GridLength(0);
        }
    }

    private void CaptureEditorRatio()
    {
        double total = EditorColumn.ActualWidth + PreviewColumn.ActualWidth;
        if (total > 0 && PreviewColumn.ActualWidth > 0)
        {
            _settings.EditorRatio = Math.Clamp(EditorColumn.ActualWidth / total, 0.1, 0.9);
        }
    }

    private void EditorTextBox_SelectionChanged(object sender, RoutedEventArgs e)
        => _viewModel.UpdateCaretPosition(EditorTextBox.CaretIndex);

    private void OnEditorScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (Math.Abs(e.VerticalChange) < 0.01) return;

        try
        {
            int firstLine = EditorTextBox.GetFirstVisibleLineIndex();
            if (firstLine < 0) return;

            int offset = EditorTextBox.GetCharacterIndexFromLineIndex(firstLine);
            if (offset >= 0) _viewModel.SyncPreviewToEditorLine(offset);
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or InvalidOperationException)
        {
            // Text layout not ready; the next scroll event will sync.
        }
    }

    /// <summary>
    /// Turns a pasted bitmap into a saved PNG plus a Markdown reference, and a
    /// pasted image file into a reference. Everything else pastes normally.
    /// </summary>
    private void OnEditorPasting(object sender, DataObjectPastingEventArgs e)
    {
        var data = e.SourceDataObject ?? e.DataObject;
        if (data is null) return;

        // Rich sources (a browser, Word) carry both text and a bitmap; text wins
        // there, so only text-free payloads are treated as an image paste.
        bool hasText = data.GetDataPresent(DataFormats.UnicodeText) || data.GetDataPresent(DataFormats.Text);
        if (hasText) return;

        try
        {
            if (data.GetDataPresent(DataFormats.FileDrop) &&
                data.GetData(DataFormats.FileDrop) is string[] files &&
                files.Any(ImageFiles.IsImageFile))
            {
                e.CancelCommand();
                _viewModel.HandleDroppedFiles(files);
                return;
            }

            if (data.GetDataPresent(DataFormats.Bitmap))
            {
                var image = data.GetData(DataFormats.Bitmap) as BitmapSource ?? ClipboardHelper.TryGetImage();
                if (image is not null)
                {
                    e.CancelCommand();
                    _viewModel.PasteImage(image);
                }
            }
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or OutOfMemoryException or NotSupportedException)
        {
            // Leave the default paste in place when the clipboard misbehaves.
        }
    }

    private void EditorTextBox_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void EditorTextBox_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;

        e.Handled = true;
        _viewModel.HandleDroppedFiles(files);
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (!_viewModel.ConfirmDiscardChanges())
        {
            e.Cancel = true;
            return;
        }

        CaptureEditorRatio();

        _settings.WindowMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            _settings.WindowWidth = ActualWidth;
            _settings.WindowHeight = ActualHeight;
        }

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.Shutdown();

        // The find dialog cancels its own Closing to stay reusable, so it has to
        // be dismissed explicitly or the process would never exit.
        _dialogs.CloseFindReplace();
        _preview.Dispose();
    }
}
