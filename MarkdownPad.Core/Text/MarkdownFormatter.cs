using System.Text;

namespace MarkdownPad.Core.Text;

/// <summary>
/// Pure text transformations behind the formatting toolbar. Every method takes
/// the document plus the current selection and returns a single
/// <see cref="EditOperation"/>, which keeps the WPF layer free of string logic
/// and makes the behaviour testable.
/// </summary>
public static class MarkdownFormatter
{
    /// <summary>
    /// Wraps the selection in <paramref name="wrapper"/> (bold, italic, strike,
    /// inline code). Applying it again to an already wrapped selection removes
    /// the markers, so the toolbar buttons toggle.
    /// </summary>
    public static EditOperation ToggleWrap(string text, int selectionStart, int selectionLength, string wrapper)
    {
        selectionStart = Clamp(selectionStart, 0, text.Length);
        selectionLength = Clamp(selectionLength, 0, text.Length - selectionStart);

        string selected = text.Substring(selectionStart, selectionLength);
        int w = wrapper.Length;

        // Markers inside the selection: "**bold**" selected whole.
        if (selected.Length >= w * 2 &&
            selected.StartsWith(wrapper, StringComparison.Ordinal) &&
            selected.EndsWith(wrapper, StringComparison.Ordinal))
        {
            string inner = selected.Substring(w, selected.Length - w * 2);
            return new EditOperation(selectionStart, selectionLength, inner, selectionStart, inner.Length);
        }

        // Markers just outside the selection: "**bold**" with only "bold" selected.
        if (selectionStart >= w && selectionStart + selectionLength + w <= text.Length &&
            string.CompareOrdinal(text, selectionStart - w, wrapper, 0, w) == 0 &&
            string.CompareOrdinal(text, selectionStart + selectionLength, wrapper, 0, w) == 0)
        {
            return new EditOperation(selectionStart - w, selectionLength + w * 2, selected, selectionStart - w, selected.Length);
        }

        string replacement = wrapper + selected + wrapper;
        return new EditOperation(selectionStart, selectionLength, replacement, selectionStart + w, selected.Length);
    }

    /// <summary>
    /// Applies a line prefix (heading, quote, bullet) to every line the selection
    /// touches. Re-applying the same prefix removes it, and a different heading
    /// level replaces the existing one instead of stacking.
    /// </summary>
    public static EditOperation ToggleLinePrefix(string text, int selectionStart, int selectionLength, string prefix)
    {
        var (start, end) = GetLineSpan(text, selectionStart, selectionLength);
        string block = text.Substring(start, end - start);
        string[] lines = block.Split('\n');

        bool allPrefixed = lines.All(line => line.TrimStart().StartsWith(prefix, StringComparison.Ordinal) || line.Trim().Length == 0);
        bool anyContent = lines.Any(line => line.Trim().Length > 0);

        var builder = new StringBuilder(block.Length + lines.Length * prefix.Length);
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) builder.Append('\n');

            string line = lines[i];
            if (allPrefixed && anyContent)
            {
                builder.Append(RemovePrefix(line, prefix));
            }
            else
            {
                builder.Append(AddPrefix(line, prefix));
            }
        }

        string replacement = builder.ToString();
        return new EditOperation(start, end - start, replacement, start, replacement.Length);
    }

    /// <summary>
    /// Numbers each selected line sequentially ("1. ", "2. ", ...). Re-applying it
    /// to an already numbered block removes the numbering.
    /// </summary>
    public static EditOperation ToggleOrderedList(string text, int selectionStart, int selectionLength)
    {
        var (start, end) = GetLineSpan(text, selectionStart, selectionLength);
        string block = text.Substring(start, end - start);
        string[] lines = block.Split('\n');

        bool allNumbered = lines.All(line => line.Trim().Length == 0 || IsOrderedItem(line));
        bool anyContent = lines.Any(line => line.Trim().Length > 0);

        var builder = new StringBuilder(block.Length + lines.Length * 4);
        int number = 1;

        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) builder.Append('\n');
            string line = lines[i];

            if (allNumbered && anyContent)
            {
                builder.Append(RemoveOrderedMarker(line));
            }
            else if (line.Trim().Length == 0)
            {
                builder.Append(line);
            }
            else
            {
                string indent = line[..(line.Length - line.TrimStart().Length)];
                builder.Append(indent).Append(number++).Append(". ").Append(line.TrimStart());
            }
        }

        string replacement = builder.ToString();
        return new EditOperation(start, end - start, replacement, start, replacement.Length);
    }

    /// <summary>
    /// Wraps the selection in a fenced code block on its own lines. With an empty
    /// selection it drops an empty fence and puts the caret inside it.
    /// </summary>
    public static EditOperation InsertCodeBlock(string text, int selectionStart, int selectionLength, string language = "")
    {
        selectionStart = Clamp(selectionStart, 0, text.Length);
        selectionLength = Clamp(selectionLength, 0, text.Length - selectionStart);

        string selected = text.Substring(selectionStart, selectionLength);
        string leading = selectionStart > 0 && text[selectionStart - 1] != '\n' ? "\n" : string.Empty;
        int selectionEnd = selectionStart + selectionLength;
        string trailing = selectionEnd < text.Length && text[selectionEnd] != '\n' ? "\n" : string.Empty;

        string opening = $"{leading}```{language}\n";
        string replacement = $"{opening}{selected}\n```{trailing}";

        int caret = selectionStart + opening.Length;
        return new EditOperation(selectionStart, selectionLength, replacement, caret, selected.Length);
    }

    /// <summary>Replaces the selection with <paramref name="snippet"/> and puts the caret after it.</summary>
    public static EditOperation InsertText(string text, int selectionStart, int selectionLength, string snippet)
    {
        selectionStart = Clamp(selectionStart, 0, text.Length);
        selectionLength = Clamp(selectionLength, 0, text.Length - selectionStart);
        return new EditOperation(selectionStart, selectionLength, snippet, selectionStart + snippet.Length, 0);
    }

    /// <summary>
    /// Builds a Markdown link, escaping the characters that would otherwise break
    /// out of the label or the destination.
    /// </summary>
    public static string BuildLink(string linkText, string url, bool isImage = false)
    {
        string label = linkText
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal);

        string destination = url.Trim();
        bool needsAngleBrackets = destination.Any(c => char.IsWhiteSpace(c) || c == '(' || c == ')');
        if (needsAngleBrackets)
        {
            destination = "<" + destination.Replace(">", "%3E", StringComparison.Ordinal) + ">";
        }

        return $"{(isImage ? "!" : string.Empty)}[{label}]({destination})";
    }

    /// <summary>Expands the selection to cover every line it touches.</summary>
    internal static (int Start, int End) GetLineSpan(string text, int selectionStart, int selectionLength)
    {
        selectionStart = Clamp(selectionStart, 0, text.Length);
        selectionLength = Clamp(selectionLength, 0, text.Length - selectionStart);

        int start = text.LastIndexOf('\n', Math.Max(0, selectionStart - 1));
        start = start < 0 || selectionStart == 0 ? 0 : start + 1;

        int searchFrom = selectionStart + selectionLength;
        int end = searchFrom >= text.Length ? -1 : text.IndexOf('\n', searchFrom);
        end = end < 0 ? text.Length : end;

        // A selection ending exactly on a line break should not pull in the next line.
        if (selectionLength > 0 && searchFrom > start && text[searchFrom - 1] == '\n')
        {
            end = searchFrom - 1;
        }

        return (start, end);
    }

    private static string AddPrefix(string line, string prefix)
    {
        string trimmed = line.TrimStart();
        string indent = line[..(line.Length - trimmed.Length)];

        // Swap one heading level for another rather than producing "## # text".
        if (prefix.StartsWith('#'))
        {
            trimmed = StripHeadingMarker(trimmed);
        }

        return indent + prefix + trimmed;
    }

    private static string RemovePrefix(string line, string prefix)
    {
        string trimmed = line.TrimStart();
        if (!trimmed.StartsWith(prefix, StringComparison.Ordinal)) return line;

        string indent = line[..(line.Length - trimmed.Length)];
        return indent + trimmed[prefix.Length..];
    }

    private static string StripHeadingMarker(string trimmed)
    {
        int hashes = 0;
        while (hashes < trimmed.Length && trimmed[hashes] == '#') hashes++;
        if (hashes == 0) return trimmed;

        int cursor = hashes;
        while (cursor < trimmed.Length && trimmed[cursor] == ' ') cursor++;
        return trimmed[cursor..];
    }

    private static bool IsOrderedItem(string line)
    {
        string trimmed = line.TrimStart();
        int digits = 0;
        while (digits < trimmed.Length && char.IsAsciiDigit(trimmed[digits])) digits++;

        return digits > 0
            && digits + 1 < trimmed.Length
            && (trimmed[digits] == '.' || trimmed[digits] == ')')
            && trimmed[digits + 1] == ' ';
    }

    private static string RemoveOrderedMarker(string line)
    {
        if (!IsOrderedItem(line)) return line;

        string trimmed = line.TrimStart();
        string indent = line[..(line.Length - trimmed.Length)];

        int cursor = 0;
        while (cursor < trimmed.Length && char.IsAsciiDigit(trimmed[cursor])) cursor++;
        cursor++; // the '.' or ')'
        if (cursor < trimmed.Length && trimmed[cursor] == ' ') cursor++;

        return indent + trimmed[cursor..];
    }

    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
}
