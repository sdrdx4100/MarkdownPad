namespace MarkdownPad.Core.Text;

/// <summary>
/// Caches the character offset where each logical line starts so that
/// caret position lookups are a binary search instead of a scan of the whole
/// document. Rebuilt when the text changes, queried on every caret move.
/// </summary>
public sealed class LineIndex
{
    private int[] _lineStarts = { 0 };
    private int _textLength;

    public int LineCount => _lineStarts.Length;

    public void Rebuild(string text)
    {
        _textLength = text.Length;

        var starts = new List<int>(Math.Max(16, text.Length / 40)) { 0 };
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }

        _lineStarts = starts.ToArray();
    }

    /// <summary>Zero-based index of the line containing <paramref name="offset"/>.</summary>
    public int GetLineIndex(int offset)
    {
        if (offset <= 0) return 0;
        if (offset > _textLength) offset = _textLength;

        int index = Array.BinarySearch(_lineStarts, offset);
        return index >= 0 ? index : ~index - 1;
    }

    public int GetLineStart(int lineIndex)
    {
        if (lineIndex <= 0) return 0;
        if (lineIndex >= _lineStarts.Length) return _lineStarts[^1];
        return _lineStarts[lineIndex];
    }

    /// <summary>One-based line and column, which is what the status bar shows.</summary>
    public (int Line, int Column) GetLineColumn(int offset)
    {
        if (offset < 0) offset = 0;
        if (offset > _textLength) offset = _textLength;

        int line = GetLineIndex(offset);
        return (line + 1, offset - _lineStarts[line] + 1);
    }

    /// <summary>Convenience for one-off lookups where no cache exists.</summary>
    public static (int Line, int Column) Compute(string text, int offset)
    {
        var index = new LineIndex();
        index.Rebuild(text);
        return index.GetLineColumn(offset);
    }
}
