namespace MarkdownPad.Core.Text;

/// <summary>Plain-text search primitives shared by the find/replace dialog.</summary>
public static class TextSearch
{
    /// <summary>
    /// Finds the next match at or after <paramref name="startIndex"/>, wrapping to
    /// the start of the document when <paramref name="wrap"/> is set.
    /// Returns -1 when there is no match.
    /// </summary>
    public static int FindNext(string text, string pattern, int startIndex, StringComparison comparison, bool wrap = true)
    {
        if (string.IsNullOrEmpty(pattern) || text.Length == 0) return -1;

        if (startIndex < 0) startIndex = 0;
        if (startIndex > text.Length) startIndex = wrap ? 0 : -1;
        if (startIndex < 0) return -1;

        int index = startIndex <= text.Length ? text.IndexOf(pattern, startIndex, comparison) : -1;
        if (index >= 0 || !wrap || startIndex == 0) return index;

        return text.IndexOf(pattern, 0, comparison);
    }

    /// <summary>
    /// Finds the last match that ends at or before <paramref name="startIndex"/>,
    /// wrapping to the end of the document. Returns -1 when there is no match.
    /// </summary>
    public static int FindPrevious(string text, string pattern, int startIndex, StringComparison comparison, bool wrap = true)
    {
        if (string.IsNullOrEmpty(pattern) || text.Length == 0) return -1;

        int limit = Math.Min(startIndex, text.Length) - pattern.Length;
        int index = limit >= 0 ? LastIndexOfUpTo(text, pattern, limit, comparison) : -1;
        if (index >= 0 || !wrap) return index;

        return LastIndexOfUpTo(text, pattern, text.Length - pattern.Length, comparison);
    }

    /// <summary>
    /// All match offsets, scanning left to right and never overlapping. Returned
    /// in ascending order; the replace-all path walks it backwards so that
    /// earlier offsets stay valid as the text is edited.
    /// </summary>
    public static IReadOnlyList<int> FindAll(string text, string pattern, StringComparison comparison)
    {
        var matches = new List<int>();
        if (string.IsNullOrEmpty(pattern) || text.Length == 0) return matches;

        int index = 0;
        while (index <= text.Length - pattern.Length)
        {
            int found = text.IndexOf(pattern, index, comparison);
            if (found < 0) break;

            matches.Add(found);
            index = found + pattern.Length;
        }

        return matches;
    }

    /// <summary>
    /// Replaces every match in one pass and reports how many there were.
    /// </summary>
    /// <remarks>
    /// The editor applies the result as a single replacement of the whole
    /// document. Replacing each match individually would be O(text x matches):
    /// every assignment copies the entire buffer, so a few thousand hits in a
    /// large file would freeze the UI for seconds.
    /// </remarks>
    public static (string Text, int Count) ReplaceAll(string text, string pattern, string replacement, StringComparison comparison)
    {
        var matches = FindAll(text, pattern, comparison);
        if (matches.Count == 0) return (text, 0);

        var builder = new System.Text.StringBuilder(text.Length + matches.Count * (replacement.Length - pattern.Length));
        int cursor = 0;

        foreach (int index in matches)
        {
            builder.Append(text, cursor, index - cursor);
            builder.Append(replacement);
            cursor = index + pattern.Length;
        }

        builder.Append(text, cursor, text.Length - cursor);
        return (builder.ToString(), matches.Count);
    }

    private static int LastIndexOfUpTo(string text, string pattern, int from, StringComparison comparison)
    {
        for (int i = Math.Min(from, text.Length - pattern.Length); i >= 0; i--)
        {
            if (string.Compare(text, i, pattern, 0, pattern.Length, comparison) == 0)
            {
                return i;
            }
        }
        return -1;
    }
}
