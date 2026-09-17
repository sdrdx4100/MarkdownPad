using System.Text;

namespace MarkdownPad.Core.Text;

/// <summary>
/// The Markdown scaffolding at the start of a line: indentation, any blockquote
/// markers, a list bullet or number, and a task checkbox.
/// </summary>
public sealed record LineStructure(
    string Indent,
    string QuotePrefix,
    string ListMarker,
    int? OrderedNumber,
    string TaskBox,
    string Content)
{
    /// <summary>Everything before the content: what a continued line repeats.</summary>
    public string Prefix => Indent + QuotePrefix + ListMarker + TaskBox;

    public bool HasListMarker => ListMarker.Length > 0;

    public bool HasQuote => QuotePrefix.Length > 0;

    /// <summary>True when the line carries structure but no text — Enter should end the list.</summary>
    public bool IsEmptyItem => Content.Length == 0 && (HasListMarker || HasQuote);

    /// <summary>
    /// The prefix for the line that follows: bullets repeat, ordered items count
    /// up, and a ticked task box starts the next item unticked.
    /// </summary>
    public string BuildNextPrefix()
    {
        var builder = new StringBuilder(Indent).Append(QuotePrefix);

        if (OrderedNumber is int number)
        {
            // "3. " follows "2. "; the separator the author used is preserved.
            char separator = ListMarker.TrimEnd().Length > 0 ? ListMarker.TrimEnd()[^1] : '.';
            builder.Append(number + 1).Append(separator).Append(' ');
        }
        else
        {
            builder.Append(ListMarker);
        }

        if (TaskBox.Length > 0) builder.Append("[ ] ");

        return builder.ToString();
    }
}

public static class LineParser
{
    /// <summary>Splits a single line into its Markdown prefix and its content.</summary>
    public static LineStructure Parse(string line)
    {
        int cursor = 0;

        while (cursor < line.Length && (line[cursor] == ' ' || line[cursor] == '\t')) cursor++;
        string indent = line[..cursor];

        int quoteStart = cursor;
        while (cursor < line.Length && line[cursor] == '>')
        {
            cursor++;
            if (cursor < line.Length && line[cursor] == ' ') cursor++;
        }
        string quotePrefix = line[quoteStart..cursor];

        // A quote may be followed by its own indentation before the list marker.
        if (quotePrefix.Length > 0)
        {
            int afterQuote = cursor;
            while (cursor < line.Length && line[cursor] == ' ') cursor++;
            quotePrefix += line[afterQuote..cursor];
        }

        var (listMarker, orderedNumber) = ParseListMarker(line, ref cursor);
        string taskBox = ParseTaskBox(line, ref cursor, listMarker.Length > 0);

        return new LineStructure(indent, quotePrefix, listMarker, orderedNumber, taskBox, line[cursor..]);
    }

    private static (string Marker, int? Number) ParseListMarker(string line, ref int cursor)
    {
        if (cursor >= line.Length) return (string.Empty, null);

        char c = line[cursor];

        if ((c == '-' || c == '*' || c == '+') && cursor + 1 < line.Length && line[cursor + 1] == ' ')
        {
            cursor += 2;
            return (line[(cursor - 2)..cursor], null);
        }

        int digitsStart = cursor;
        while (cursor < line.Length && char.IsAsciiDigit(line[cursor])) cursor++;

        if (cursor > digitsStart &&
            cursor + 1 < line.Length &&
            (line[cursor] == '.' || line[cursor] == ')') &&
            line[cursor + 1] == ' ')
        {
            cursor += 2;
            string marker = line[digitsStart..cursor];
            return (marker, int.TryParse(line[digitsStart..(cursor - 2)], out int n) ? n : 0);
        }

        cursor = digitsStart;
        return (string.Empty, null);
    }

    private static string ParseTaskBox(string line, ref int cursor, bool hasListMarker)
    {
        if (!hasListMarker || cursor + 3 > line.Length) return string.Empty;
        if (line[cursor] != '[' || line[cursor + 2] != ']') return string.Empty;

        char state = line[cursor + 1];
        if (state is not (' ' or 'x' or 'X')) return string.Empty;

        int length = cursor + 3 < line.Length && line[cursor + 3] == ' ' ? 4 : 3;
        string box = line.Substring(cursor, length);
        cursor += length;

        return box;
    }
}
