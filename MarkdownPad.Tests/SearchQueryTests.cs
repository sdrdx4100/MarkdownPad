using MarkdownPad.Core.Text;
using Xunit;

namespace MarkdownPad.Tests;

public class SearchQueryTests
{
    private static SearchQuery Plain(string pattern, bool matchCase = false)
        => new(pattern, matchCase, useRegex: false);

    private static SearchQuery Regex(string pattern, bool matchCase = false)
        => new(pattern, matchCase, useRegex: true);

    [Fact]
    public void An_invalid_regex_reports_an_error_instead_of_throwing()
    {
        var query = Regex("[unclosed");

        Assert.NotNull(query.Error);
        Assert.False(query.IsUsable);
        Assert.Null(TextSearch.FindNext("text", query, 0));
        Assert.Empty(TextSearch.FindAll("text", query));
    }

    [Fact]
    public void Regex_matches_carry_their_own_length()
    {
        var match = TextSearch.FindNext("foo12345bar", Regex(@"\d+"), 0);

        Assert.Equal(new SearchMatch(3, 5), match);
    }

    [Fact]
    public void Regex_find_all_never_stalls_on_zero_width_matches()
    {
        var matches = TextSearch.FindAll("abc", Regex("x*"));

        // One empty match per position, and the scan terminates.
        Assert.Equal(4, matches.Count);
        Assert.All(matches, m => Assert.Equal(0, m.Length));
    }

    [Fact]
    public void Regex_replacement_supports_group_substitution()
    {
        var (text, count) = TextSearch.ReplaceAll("a=1, b=2", Regex(@"(\w)=(\d)"), "$2=$1");

        Assert.Equal("1=a, 2=b", text);
        Assert.Equal(2, count);
    }

    [Fact]
    public void Regex_honours_case_sensitivity()
    {
        Assert.Null(TextSearch.FindNext("ABC", Regex("abc", matchCase: true), 0));
        Assert.NotNull(TextSearch.FindNext("ABC", Regex("abc"), 0));
    }

    [Fact]
    public void Regex_find_previous_walks_backwards_and_wraps()
    {
        const string text = "a1 b22 c333";
        var query = Regex(@"\d+");

        Assert.Equal(new SearchMatch(4, 2), TextSearch.FindPrevious(text, query, 6));
        Assert.Equal(new SearchMatch(8, 3), TextSearch.FindPrevious(text, query, 0));
    }

    [Fact]
    public void Plain_queries_go_through_the_same_surface()
    {
        const string text = "cat CAT cat";

        Assert.Equal(3, TextSearch.FindAll(text, Plain("cat")).Count);
        Assert.Equal(2, TextSearch.FindAll(text, Plain("cat", matchCase: true)).Count);

        var (replaced, count) = TextSearch.ReplaceAll(text, Plain("cat"), "dog");
        Assert.Equal("dog dog dog", replaced);
        Assert.Equal(3, count);
    }

    [Fact]
    public void An_empty_pattern_finds_nothing_in_either_mode()
    {
        Assert.False(Plain("").IsUsable);
        Assert.False(Regex("").IsUsable);
        Assert.Null(TextSearch.FindNext("abc", Plain(""), 0));
    }

    [Fact]
    public void Regex_anchors_work_per_line()
    {
        var matches = TextSearch.FindAll("one\ntwo\nthree", Regex("^t"));
        Assert.Equal(2, matches.Count);
    }
}
