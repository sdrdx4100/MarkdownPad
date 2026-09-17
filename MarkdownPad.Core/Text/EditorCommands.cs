namespace MarkdownPad.Core.Text;

/// <summary>
/// The editing conveniences a Markdown editor is expected to have: continuing a
/// list on Enter, indenting a block with Tab, moving and duplicating lines.
/// All of them are pure transformations so the WPF layer only has to bind keys.
/// </summary>
public static class EditorCommands
{
    public const string DefaultIndent = "  ";

    /// <summary>
    /// Handles Enter. Returns the edit to apply, or <c>null</c> to let the editor
    /// insert a plain newline.
    /// </summary>
    /// <remarks>
    /// Continuing "- " or "3. " onto the next line, and clearing the marker when
    /// Enter is pressed on an empty item, is the single most expected behaviour
    /// in a Markdown editor. Indentation is carried over for ordinary lines.
    /// </remarks>
    public static EditOperation? ContinueLine(string text, int caret, string newLine = "\n")
    {
        caret = Math.Clamp(caret, 0, text.Length);

        int lineStart = LineStartOf(text, caret);
        string beforeCaret = text[lineStart..caret];

        // Only continue when the caret sits at the end of the line's content;
        // splitting a line in the middle should behave normally.
        int lineEnd = LineEndOf(text, caret);
        if (text[caret..lineEnd].Trim().Length > 0) return null;

        var structure = LineParser.Parse(beforeCaret);

        if (structure.IsEmptyItem)
        {
            // Second Enter on an empty item leaves the list rather than nesting.
            return new EditOperation(lineStart, caret - lineStart, string.Empty, lineStart, 0);
        }

        string prefix = structure.HasListMarker || structure.HasQuote
            ? structure.BuildNextPrefix()
            : structure.Indent;

        if (prefix.Length == 0) return null;

        string insertion = newLine + prefix;
        return new EditOperation(caret, 0, insertion, caret + insertion.Length, 0);
    }

    /// <summary>
    /// Handles Tab. A multi-line selection is indented as a block; otherwise the
    /// indent is simply inserted, which is what every editor does.
    /// </summary>
    public static EditOperation Indent(string text, int selectionStart, int selectionLength, string indent = DefaultIndent)
    {
        if (!SpansMultipleLines(text, selectionStart, selectionLength))
        {
            return MarkdownFormatter.InsertText(text, selectionStart, selectionLength, indent);
        }

        var (start, end) = MarkdownFormatter.GetLineSpan(text, selectionStart, selectionLength);
        string block = text[start..end];

        string replacement = string.Join('\n', block.Split('\n').Select(line => line.Length == 0 ? line : indent + line));
        return new EditOperation(start, end - start, replacement, start, replacement.Length);
    }

    /// <summary>Handles Shift+Tab: removes one indent level from every touched line.</summary>
    public static EditOperation Outdent(string text, int selectionStart, int selectionLength, string indent = DefaultIndent)
    {
        var (start, end) = MarkdownFormatter.GetLineSpan(text, selectionStart, selectionLength);
        string block = text[start..end];

        string replacement = string.Join('\n', block.Split('\n').Select(line => RemoveOneIndent(line, indent.Length)));
        return new EditOperation(start, end - start, replacement, start, replacement.Length);
    }

    /// <summary>Moves the touched lines up (-1) or down (+1), keeping them selected.</summary>
    public static EditOperation? MoveLines(string text, int selectionStart, int selectionLength, int direction)
    {
        var (start, end) = MarkdownFormatter.GetLineSpan(text, selectionStart, selectionLength);
        string block = text[start..end];

        if (direction < 0)
        {
            if (start == 0) return null;

            int previousStart = LineStartOf(text, start - 1);
            string previous = text[previousStart..(start - 1)];

            string replacement = block + "\n" + previous;
            return new EditOperation(previousStart, end - previousStart, replacement, previousStart, block.Length);
        }

        if (end >= text.Length) return null;

        int nextEnd = LineEndOf(text, end + 1);
        string next = text[(end + 1)..nextEnd];

        string moved = next + "\n" + block;
        return new EditOperation(start, nextEnd - start, moved, start + next.Length + 1, block.Length);
    }

    /// <summary>Duplicates the touched lines below themselves.</summary>
    public static EditOperation DuplicateLines(string text, int selectionStart, int selectionLength)
    {
        var (start, end) = MarkdownFormatter.GetLineSpan(text, selectionStart, selectionLength);
        string block = text[start..end];

        string replacement = block + "\n" + block;
        return new EditOperation(start, end - start, replacement, end + 1, block.Length);
    }

    /// <summary>Deletes the touched lines entirely.</summary>
    public static EditOperation DeleteLines(string text, int selectionStart, int selectionLength)
    {
        var (start, end) = MarkdownFormatter.GetLineSpan(text, selectionStart, selectionLength);

        // Take the trailing newline with the block, or the leading one on the last line.
        int removeEnd = end < text.Length ? end + 1 : end;
        int removeStart = removeEnd == end && start > 0 ? start - 1 : start;

        return new EditOperation(removeStart, removeEnd - removeStart, string.Empty, removeStart, 0);
    }

    private static string RemoveOneIndent(string line, int width)
    {
        if (line.StartsWith('\t')) return line[1..];

        int removable = 0;
        while (removable < width && removable < line.Length && line[removable] == ' ') removable++;

        return line[removable..];
    }

    private static bool SpansMultipleLines(string text, int selectionStart, int selectionLength)
    {
        if (selectionLength <= 0) return false;

        int start = Math.Clamp(selectionStart, 0, text.Length);
        int length = Math.Clamp(selectionLength, 0, text.Length - start);

        return text.AsSpan(start, length).Contains('\n');
    }

    private static int LineStartOf(string text, int offset)
    {
        if (offset <= 0) return 0;

        int index = text.LastIndexOf('\n', Math.Min(offset, text.Length) - 1);
        return index < 0 ? 0 : index + 1;
    }

    private static int LineEndOf(string text, int offset)
    {
        if (offset >= text.Length) return text.Length;

        int index = text.IndexOf('\n', offset);
        return index < 0 ? text.Length : index;
    }
}
