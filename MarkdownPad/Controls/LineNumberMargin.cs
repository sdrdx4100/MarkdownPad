using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarkdownPad.Core.Text;

namespace MarkdownPad.Controls;

/// <summary>
/// Draws logical line numbers alongside a <see cref="TextBox"/>.
/// </summary>
/// <remarks>
/// The "行番号を表示" menu item previously toggled a property that nothing
/// consumed. This renders the gutter for real. With word wrap on, the TextBox
/// counts display lines, so a number is drawn only on the display line that
/// actually starts a logical line.
/// </remarks>
public sealed class LineNumberMargin : FrameworkElement
{
    private const double HorizontalPadding = 8;

    private readonly LineIndex _lineIndex = new();
    private TextBox? _editor;
    private int _caretLine = -1;

    public LineNumberMargin()
    {
        ClipToBounds = true;
        SnapsToDevicePixels = true;
    }

    public static readonly DependencyProperty EditorProperty = DependencyProperty.Register(
        nameof(Editor), typeof(TextBox), typeof(LineNumberMargin),
        new PropertyMetadata(null, OnEditorChanged));

    public TextBox? Editor
    {
        get => (TextBox?)GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground), typeof(Brush), typeof(LineNumberMargin),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public static readonly DependencyProperty CurrentLineBrushProperty = DependencyProperty.Register(
        nameof(CurrentLineBrush), typeof(Brush), typeof(LineNumberMargin),
        new FrameworkPropertyMetadata(Brushes.DimGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush CurrentLineBrush
    {
        get => (Brush)GetValue(CurrentLineBrushProperty);
        set => SetValue(CurrentLineBrushProperty, value);
    }

    private static void OnEditorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var margin = (LineNumberMargin)d;
        margin.Detach(e.OldValue as TextBox);
        margin.Attach(e.NewValue as TextBox);
    }

    private void Attach(TextBox? editor)
    {
        _editor = editor;
        if (editor is null) return;

        editor.TextChanged += OnTextChanged;
        editor.SelectionChanged += OnSelectionChanged;
        editor.SizeChanged += OnEditorInvalidated;
        editor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnScrollChanged));

        Rebuild();
    }

    private void Detach(TextBox? editor)
    {
        if (editor is null) return;

        editor.TextChanged -= OnTextChanged;
        editor.SelectionChanged -= OnSelectionChanged;
        editor.SizeChanged -= OnEditorInvalidated;
        editor.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnScrollChanged));
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e) => Rebuild();

    private void OnEditorInvalidated(object sender, SizeChangedEventArgs e) => InvalidateVisual();

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e) => InvalidateVisual();

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (_editor is null) return;

        int line = _lineIndex.GetLineIndex(_editor.CaretIndex);
        if (line == _caretLine) return;

        _caretLine = line;
        InvalidateVisual();
    }

    private void Rebuild()
    {
        if (_editor is null) return;

        _lineIndex.Rebuild(_editor.Text);
        _caretLine = _lineIndex.GetLineIndex(_editor.CaretIndex);

        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_editor is null) return new Size(0, 0);

        // Size to the widest number the document can currently produce, so the
        // gutter does not jitter as the caret moves.
        var sample = CreateText(new string('0', Math.Max(2, _lineIndex.LineCount.ToString().Length)), Foreground);
        return new Size(sample.Width + HorizontalPadding * 2, 0);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var editor = _editor;
        if (editor is null || editor.ActualHeight <= 0) return;

        int firstVisible, lastVisible;
        try
        {
            firstVisible = editor.GetFirstVisibleLineIndex();
            lastVisible = editor.GetLastVisibleLineIndex();
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentOutOfRangeException)
        {
            // The text layout is not available yet (the control is still loading).
            return;
        }

        if (firstVisible < 0 || lastVisible < firstVisible) return;

        for (int displayLine = firstVisible; displayLine <= lastVisible; displayLine++)
        {
            int characterIndex;
            Rect rect;

            try
            {
                characterIndex = editor.GetCharacterIndexFromLineIndex(displayLine);
                if (characterIndex < 0) continue;

                rect = editor.GetRectFromCharacterIndex(characterIndex);
            }
            catch (Exception e) when (e is ArgumentOutOfRangeException or InvalidOperationException)
            {
                continue;
            }

            if (double.IsInfinity(rect.Top) || double.IsNaN(rect.Top)) continue;

            int logicalLine = _lineIndex.GetLineIndex(characterIndex);

            // With wrapping on, only the first display line of a logical line is numbered.
            if (characterIndex != _lineIndex.GetLineStart(logicalLine)) continue;

            var brush = logicalLine == _caretLine ? CurrentLineBrush : Foreground;
            var text = CreateText((logicalLine + 1).ToString(CultureInfo.InvariantCulture), brush);

            drawingContext.DrawText(text, new Point(ActualWidth - text.Width - HorizontalPadding, rect.Top));
        }
    }

    private FormattedText CreateText(string value, Brush brush)
    {
        var editor = _editor;
        var typeface = editor is null
            ? new Typeface("Consolas")
            : new Typeface(editor.FontFamily, editor.FontStyle, editor.FontWeight, editor.FontStretch);

        return new FormattedText(
            value,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            editor?.FontSize ?? 14,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }
}
