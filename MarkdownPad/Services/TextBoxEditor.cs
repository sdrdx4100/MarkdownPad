using System.Windows.Controls;
using MarkdownPad.Core.Text;

namespace MarkdownPad.Services;

/// <summary>
/// Thin wrapper that lets the view model drive the editor without knowing it is
/// a <see cref="TextBox"/>, and that guarantees every edit is applied as a
/// single undoable change.
/// </summary>
public sealed class TextBoxEditor
{
    private readonly TextBox _textBox;

    public TextBoxEditor(TextBox textBox) => _textBox = textBox;

    public string Text => _textBox.Text;

    public int SelectionStart => _textBox.SelectionStart;

    public int SelectionLength => _textBox.SelectionLength;

    public string SelectedText => _textBox.SelectedText;

    public int CaretIndex => _textBox.CaretIndex;

    public void Focus() => _textBox.Focus();

    public void Select(int start, int length)
    {
        int safeStart = Math.Clamp(start, 0, _textBox.Text.Length);
        int safeLength = Math.Clamp(length, 0, _textBox.Text.Length - safeStart);
        _textBox.Select(safeStart, safeLength);
    }

    /// <summary>Applies one formatting operation as a single undo step.</summary>
    public void Apply(EditOperation operation)
    {
        _textBox.BeginChange();
        try
        {
            Select(operation.Start, operation.Length);
            _textBox.SelectedText = operation.ReplacementText;
            Select(operation.SelectionStart, operation.SelectionLength);
        }
        finally
        {
            _textBox.EndChange();
        }

        _textBox.Focus();
    }

    public void InsertAtCaret(string text)
        => Apply(MarkdownFormatter.InsertText(Text, SelectionStart, SelectionLength, text));

    /// <summary>Brings <paramref name="offset"/> into view without stealing focus.</summary>
    public void ScrollToOffset(int offset)
    {
        try
        {
            int line = _textBox.GetLineIndexFromCharacterIndex(Math.Clamp(offset, 0, _textBox.Text.Length));
            if (line >= 0) _textBox.ScrollToLine(line);
        }
        catch (ArgumentOutOfRangeException)
        {
            // Layout has not caught up with the text yet; the next scroll will.
        }
    }
}
