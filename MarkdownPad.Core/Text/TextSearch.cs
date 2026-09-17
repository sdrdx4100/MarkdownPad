using System.Text.RegularExpressions;

namespace MarkdownPad.Core.Text;

/// <summary>Search primitives shared by the find/replace dialog.</summary>
public static class TextSearch
{
    /// <summary>Next match at or after <paramref name="startIndex"/>, honouring the query's mode.</summary>
    public static SearchMatch? FindNext(string text, SearchQuery query, int startIndex, bool wrap = true)
    {
        if (!query.IsUsable || text.Length == 0) return null;

        if (query.Regex is null)
        {
            int index = FindNext(text, query.Pattern, startIndex, query.Comparison, wrap);
            return index < 0 ? null : new SearchMatch(index, query.Pattern.Length);
        }

        int from = Math.Clamp(startIndex, 0, text.Length);
        var match = SafeMatch(query.Regex, text, from);

        if ((match is null || !match.Success) && wrap && from > 0)
        {
            match = SafeMatch(query.Regex, text, 0);
        }

        return match is { Success: true } ? new SearchMatch(match.Index, match.Length) : null;
    }

    /// <summary>Last match ending at or before <paramref name="startIndex"/>.</summary>
    public static SearchMatch? FindPrevious(string text, SearchQuery query, int startIndex, bool wrap = true)
    {
        if (!query.IsUsable || text.Length == 0) return null;

        if (query.Regex is null)
        {
            int index = FindPrevious(text, query.Pattern, startIndex, query.Comparison, wrap);
            return index < 0 ? null : new SearchMatch(index, query.Pattern.Length);
        }

        var matches = FindAll(text, query);
        if (matches.Count == 0) return null;

        for (int i = matches.Count - 1; i >= 0; i--)
        {
            if (matches[i].Index + matches[i].Length <= startIndex) return matches[i];
        }

        return wrap ? matches[^1] : null;
    }

    /// <summary>Every non-overlapping match, in ascending order.</summary>
    public static IReadOnlyList<SearchMatch> FindAll(string text, SearchQuery query)
    {
        if (!query.IsUsable || text.Length == 0) return Array.Empty<SearchMatch>();

        if (query.Regex is null)
        {
            return FindAll(text, query.Pattern, query.Comparison)
                .Select(index => new SearchMatch(index, query.Pattern.Length))
                .ToArray();
        }

        var results = new List<SearchMatch>();
        int cursor = 0;

        while (cursor <= text.Length)
        {
            var match = SafeMatch(query.Regex, text, cursor);
            if (match is not { Success: true }) break;

            results.Add(new SearchMatch(match.Index, match.Length));

            // A zero-width match would otherwise spin forever on one position.
            cursor = match.Length > 0 ? match.Index + match.Length : match.Index + 1;
        }

        return results;
    }

    /// <summary>
    /// Replaces every match in one pass. Regular-expression replacements support
    /// the usual <c>$1</c> group substitutions.
    /// </summary>
    public static (string Text, int Count) ReplaceAll(string text, SearchQuery query, string replacement)
    {
        if (!query.IsUsable) return (text, 0);

        if (query.Regex is null)
        {
            return ReplaceAll(text, query.Pattern, replacement, query.Comparison);
        }

        var matches = FindAll(text, query);
        if (matches.Count == 0) return (text, 0);

        var builder = new System.Text.StringBuilder(text.Length);
        int cursor = 0;

        foreach (var match in matches)
        {
            builder.Append(text, cursor, match.Index - cursor);

            var regexMatch = query.Regex.Match(text, match.Index, match.Length);
            builder.Append(regexMatch.Success ? regexMatch.Result(replacement) : replacement);

            cursor = match.Index + match.Length;
        }

        builder.Append(text, cursor, text.Length - cursor);
        return (builder.ToString(), matches.Count);
    }

    /// <summary>
    /// Runs a regular expression, turning a runaway pattern into "no match"
    /// instead of an exception on the UI thread.
    /// </summary>
    private static Match? SafeMatch(Regex regex, string text, int startAt)
    {
        try
        {
            return regex.Match(text, startAt);
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

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
