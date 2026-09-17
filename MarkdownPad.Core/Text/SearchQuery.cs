using System.Text.RegularExpressions;

namespace MarkdownPad.Core.Text;

/// <summary>A located match: regular expressions make the length vary.</summary>
public readonly record struct SearchMatch(int Index, int Length);

/// <summary>
/// A compiled search request. Regular expressions are compiled once, with a
/// match timeout, so that a pathological pattern reports failure instead of
/// locking up the UI thread.
/// </summary>
public sealed class SearchQuery
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    private readonly Regex? _regex;

    public SearchQuery(string pattern, bool matchCase, bool useRegex)
    {
        Pattern = pattern ?? string.Empty;
        MatchCase = matchCase;
        UseRegex = useRegex;

        if (!useRegex || Pattern.Length == 0) return;

        try
        {
            var options = RegexOptions.Multiline;
            if (!matchCase) options |= RegexOptions.IgnoreCase;

            _regex = new Regex(Pattern, options, MatchTimeout);
        }
        catch (ArgumentException e)
        {
            Error = e.Message;
        }
    }

    public string Pattern { get; }

    public bool MatchCase { get; }

    public bool UseRegex { get; }

    /// <summary>Set when a regular expression failed to compile.</summary>
    public string? Error { get; }

    public bool IsUsable => Error is null && Pattern.Length > 0;

    public StringComparison Comparison =>
        MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    internal Regex? Regex => _regex;
}
