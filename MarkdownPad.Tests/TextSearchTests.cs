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

    [Theory]
    [InlineData("cat cat cat", "cat", "dog", "dog dog dog", 3)]
    [InlineData("aaaa", "aa", "b", "bb", 2)]
    [InlineData("abc", "x", "y", "abc", 0)]
    [InlineData("aaa", "a", "aa", "aaaaaa", 3)]
    [InlineData("hello", "", "x", "hello", 0)]
    [InlineData("abc", "b", "", "ac", 1)]
    public void Replace_all_rewrites_the_document_in_one_pass(
        string text, string pattern, string replacement, string expected, int expectedCount)
    {
        var (result, count) = TextSearch.ReplaceAll(text, pattern, replacement, StringComparison.Ordinal);

        Assert.Equal(expected, result);
        Assert.Equal(expectedCount, count);
    }

    [Fact]
    public void Replace_all_never_rescans_its_own_output()
    {
        // Replacing "a" with "aa" must not cascade: exactly one pass, three hits.
        var (result, count) = TextSearch.ReplaceAll("a b a b a", "a", "aa", StringComparison.Ordinal);

        Assert.Equal("aa b aa b aa", result);
        Assert.Equal(3, count);
    }

    [Fact]
    public void Replace_all_honours_case_insensitivity()
    {
        var (result, count) = TextSearch.ReplaceAll("Cat cat CAT", "cat", "dog", StringComparison.OrdinalIgnoreCase);

        Assert.Equal("dog dog dog", result);
        Assert.Equal(3, count);
    }

    [Fact]
    public void Replace_all_stays_linear_on_a_large_document()
    {
        // 20k matches in a ~1 MB buffer. The previous per-match approach copied
        // the whole buffer each time; this must finish effectively instantly.
        string text = string.Concat(Enumerable.Repeat("needle" + new string('x', 44), 20_000));

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var (result, count) = TextSearch.ReplaceAll(text, "needle", "pin", StringComparison.Ordinal);
        watch.Stop();

        Assert.Equal(20_000, count);
        Assert.DoesNotContain("needle", result);
        Assert.True(watch.ElapsedMilliseconds < 2000, $"ReplaceAll took {watch.ElapsedMilliseconds} ms");
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
