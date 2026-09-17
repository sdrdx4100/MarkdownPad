using MarkdownPad.Core.Text;
using Xunit;

namespace MarkdownPad.Tests;

public class MarkdownFormatterTests
{
    private static string Apply(string text, EditOperation op)
        => text[..op.Start] + op.ReplacementText + text[(op.Start + op.Length)..];

    [Fact]
    public void Wrap_adds_markers_around_the_selection()
    {
        const string text = "hello world";
        var op = MarkdownFormatter.ToggleWrap(text, 6, 5, "**");

        Assert.Equal("hello **world**", Apply(text, op));
        Assert.Equal(8, op.SelectionStart);
        Assert.Equal(5, op.SelectionLength);
    }

    [Fact]
    public void Wrap_with_empty_selection_places_the_caret_between_the_markers()
    {
        const string text = "ab";
        var op = MarkdownFormatter.ToggleWrap(text, 1, 0, "**");

        Assert.Equal("a****b", Apply(text, op));
        Assert.Equal(3, op.SelectionStart);
        Assert.Equal(0, op.SelectionLength);
    }

    [Fact]
    public void Wrap_removes_markers_when_they_are_inside_the_selection()
    {
        const string text = "hello **world**";
        var op = MarkdownFormatter.ToggleWrap(text, 6, 9, "**");

        Assert.Equal("hello world", Apply(text, op));
    }

    [Fact]
    public void Wrap_removes_markers_when_they_surround_the_selection()
    {
        const string text = "hello **world**";
        var op = MarkdownFormatter.ToggleWrap(text, 8, 5, "**");

        Assert.Equal("hello world", Apply(text, op));
    }

    [Fact]
    public void Line_prefix_applies_to_every_selected_line()
    {
        const string text = "one\ntwo\nthree";
        var op = MarkdownFormatter.ToggleLinePrefix(text, 0, text.Length, "- ");

        Assert.Equal("- one\n- two\n- three", Apply(text, op));
    }

    [Fact]
    public void Line_prefix_toggles_off_when_every_line_already_has_it()
    {
        const string text = "- one\n- two";
        var op = MarkdownFormatter.ToggleLinePrefix(text, 0, text.Length, "- ");

        Assert.Equal("one\ntwo", Apply(text, op));
    }

    [Fact]
    public void Heading_replaces_an_existing_level_instead_of_stacking()
    {
        const string text = "# title";
        var op = MarkdownFormatter.ToggleLinePrefix(text, 0, text.Length, "### ");

        Assert.Equal("### title", Apply(text, op));
    }

    [Fact]
    public void Line_prefix_touches_only_the_lines_the_selection_covers()
    {
        const string text = "one\ntwo\nthree";
        // Caret inside "two" only.
        var op = MarkdownFormatter.ToggleLinePrefix(text, 5, 0, "> ");

        Assert.Equal("one\n> two\nthree", Apply(text, op));
    }

    [Fact]
    public void Selection_ending_on_a_newline_does_not_pull_in_the_next_line()
    {
        const string text = "one\ntwo\nthree";
        var op = MarkdownFormatter.ToggleLinePrefix(text, 0, 4, "- ");

        Assert.Equal("- one\ntwo\nthree", Apply(text, op));
    }

    [Fact]
    public void Ordered_list_numbers_lines_sequentially()
    {
        const string text = "alpha\nbravo\ncharlie";
        var op = MarkdownFormatter.ToggleOrderedList(text, 0, text.Length);

        Assert.Equal("1. alpha\n2. bravo\n3. charlie", Apply(text, op));
    }

    [Fact]
    public void Ordered_list_toggles_off()
    {
        const string text = "1. alpha\n2. bravo";
        var op = MarkdownFormatter.ToggleOrderedList(text, 0, text.Length);

        Assert.Equal("alpha\nbravo", Apply(text, op));
    }

    [Fact]
    public void Code_block_fences_the_selection_on_its_own_lines()
    {
        const string text = "before\ncode here\nafter";
        var op = MarkdownFormatter.InsertCodeBlock(text, 7, 9);

        Assert.Equal("before\n```\ncode here\n```\nafter", Apply(text, op));
    }

    [Theory]
    [InlineData("text", "http://a.example/b", "[text](http://a.example/b)")]
    [InlineData("a]b", "u", @"[a\]b](u)")]
    [InlineData("t", "with space.png", "[t](<with space.png>)")]
    public void Builds_escaped_links(string label, string url, string expected)
    {
        Assert.Equal(expected, MarkdownFormatter.BuildLink(label, url));
    }

    [Fact]
    public void Builds_image_links()
    {
        Assert.Equal("![shot](images/shot.png)", MarkdownFormatter.BuildLink("shot", "images/shot.png", isImage: true));
    }
}
