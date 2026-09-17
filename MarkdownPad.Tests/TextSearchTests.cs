using MarkdownPad.Core.Text;
using Xunit;

namespace MarkdownPad.Tests;

public class TextSearchTests
{
    [Fact]
    public void Find_next_wraps_to_the_beginning()
    {
        const string text = "abc abc";

        Assert.Equal(4, TextSearch.FindNext(text, "abc", 1, StringComparison.Ordinal));
        Assert.Equal(0, TextSearch.FindNext(text, "abc", 5, StringComparison.Ordinal));
    }

    [Fact]
    public void Find_next_respects_case_sensitivity()
    {
        Assert.Equal(-1, TextSearch.FindNext("ABC", "abc", 0, StringComparison.Ordinal));
        Assert.Equal(0, TextSearch.FindNext("ABC", "abc", 0, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Find_previous_walks_backwards_and_wraps()
    {
        const string text = "abc abc abc";

        Assert.Equal(4, TextSearch.FindPrevious(text, "abc", 8, StringComparison.Ordinal));
        Assert.Equal(8, TextSearch.FindPrevious(text, "abc", 0, StringComparison.Ordinal));
    }

    [Fact]
    public void Find_all_returns_non_overlapping_matches_in_order()
    {
        Assert.Equal(new[] { 0, 2 }, TextSearch.FindAll("aaaa", "aa", StringComparison.Ordinal));
        Assert.Equal(new[] { 0, 3, 6 }, TextSearch.FindAll("abcabcabc", "abc", StringComparison.Ordinal));
    }

    [Fact]
    public void Find_all_on_an_empty_pattern_is_empty_rather_than_infinite()
    {
        Assert.Empty(TextSearch.FindAll("abc", "", StringComparison.Ordinal));
    }

    [Fact]
    public void Replacing_backwards_keeps_earlier_offsets_valid()
    {
        const string text = "cat cat cat";
        var matches = TextSearch.FindAll(text, "cat", StringComparison.Ordinal);

        // This mirrors what the editor does: walk the matches from the end so
        // that replacing one never shifts the offsets still to be processed.
        string result = text;
        foreach (int index in matches.Reverse())
        {
            result = result[..index] + "dog" + result[(index + 3)..];
        }

        Assert.Equal("dog dog dog", result);
    }
}
