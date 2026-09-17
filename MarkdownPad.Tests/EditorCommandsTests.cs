using MarkdownPad.Core.Text;
using Xunit;

namespace MarkdownPad.Tests;

public class EditorCommandsTests
{
    private static string Apply(string text, EditOperation? op)
        => op is null ? text : text[..op.Start] + op.ReplacementText + text[(op.Start + op.Length)..];

    [Theory]
    [InlineData("- item", "- item\n- ")]
    [InlineData("* item", "* item\n* ")]
    [InlineData("+ item", "+ item\n+ ")]
    [InlineData("1. item", "1. item\n2. ")]
    [InlineData("7) item", "7) item\n8) ")]
    [InlineData("> quoted", "> quoted\n> ")]
    [InlineData("  - nested", "  - nested\n  - ")]
    [InlineData("    indented", "    indented\n    ")]
    public void Enter_continues_the_current_construct(string line, string expected)
    {
        var op = EditorCommands.ContinueLine(line, line.Length);
        Assert.Equal(expected, Apply(line, op));
    }

    [Theory]
    [InlineData("- [ ] task", "- [ ] task\n- [ ] ")]
    [InlineData("- [x] done", "- [x] done\n- [ ] ")]
    public void Enter_starts_the_next_task_unticked(string line, string expected)
    {
        var op = EditorCommands.ContinueLine(line, line.Length);
        Assert.Equal(expected, Apply(line, op));
    }

    [Theory]
    [InlineData("- ")]
    [InlineData("1. ")]
    [InlineData("> ")]
    [InlineData("- [ ] ")]
    [InlineData("  - ")]
    public void Enter_on_an_empty_item_leaves_the_list(string line)
    {
        string text = "before\n" + line;
        var op = EditorCommands.ContinueLine(text, text.Length);

        Assert.Equal("before\n", Apply(text, op));
    }

    [Fact]
    public void Enter_on_a_plain_line_defers_to_the_editor()
    {
        Assert.Null(EditorCommands.ContinueLine("plain text", 10));
        Assert.Null(EditorCommands.ContinueLine("", 0));
    }

    [Fact]
    public void Enter_in_the_middle_of_a_line_defers_to_the_editor()
    {
        Assert.Null(EditorCommands.ContinueLine("- item text", 4));
    }

    [Fact]
    public void Enter_uses_the_requested_line_ending()
    {
        var op = EditorCommands.ContinueLine("- a", 3, "\r\n");
        Assert.Equal("- a\r\n- ", Apply("- a", op));
    }

    [Fact]
    public void Ordered_numbering_continues_from_the_current_value()
    {
        const string text = "1. one\n2. two";
        var op = EditorCommands.ContinueLine(text, text.Length);

        Assert.Equal("1. one\n2. two\n3. ", Apply(text, op));
    }

    [Fact]
    public void Tab_indents_every_line_of_a_multi_line_selection()
    {
        const string text = "one\ntwo\nthree";
        var op = EditorCommands.Indent(text, 0, text.Length);

        Assert.Equal("  one\n  two\n  three", Apply(text, op));
    }

    [Fact]
    public void Tab_on_a_single_line_inserts_the_indent_at_the_caret()
    {
        const string text = "ab";
        var op = EditorCommands.Indent(text, 1, 0);

        Assert.Equal("a  b", Apply(text, op));
    }

    [Fact]
    public void Shift_tab_removes_one_level_and_tolerates_partial_indentation()
    {
        const string text = "    four\n one\nnone";
        var op = EditorCommands.Outdent(text, 0, text.Length);

        Assert.Equal("  four\none\nnone", Apply(text, op));
    }

    [Fact]
    public void Indent_leaves_blank_lines_alone()
    {
        const string text = "one\n\ntwo";
        var op = EditorCommands.Indent(text, 0, text.Length);

        Assert.Equal("  one\n\n  two", Apply(text, op));
    }

    [Fact]
    public void Lines_move_up_and_down()
    {
        const string text = "a\nb\nc";

        Assert.Equal("b\na\nc", Apply(text, EditorCommands.MoveLines(text, 2, 0, -1)));
        Assert.Equal("b\na\nc", Apply(text, EditorCommands.MoveLines(text, 0, 0, +1)));
    }

    [Fact]
    public void Moving_a_block_keeps_it_together()
    {
        const string text = "a\nb\nc\nd";
        // Select lines "b" and "c", move down past "d".
        var op = EditorCommands.MoveLines(text, 2, 3, +1);

        Assert.Equal("a\nd\nb\nc", Apply(text, op));
    }

    [Fact]
    public void Moving_past_the_edges_is_a_no_op()
    {
        const string text = "a\nb";

        Assert.Null(EditorCommands.MoveLines(text, 0, 0, -1));
        Assert.Null(EditorCommands.MoveLines(text, 2, 0, +1));
    }

    [Fact]
    public void Lines_duplicate_below_themselves()
    {
        const string text = "a\nb";
        Assert.Equal("a\na\nb", Apply(text, EditorCommands.DuplicateLines(text, 0, 0)));
    }

    [Fact]
    public void Deleting_a_line_removes_its_newline_too()
    {
        Assert.Equal("b\nc", Apply("a\nb\nc", EditorCommands.DeleteLines("a\nb\nc", 0, 0)));
        Assert.Equal("a\nb", Apply("a\nb\nc", EditorCommands.DeleteLines("a\nb\nc", 4, 0)));
    }
}

public class LineParserTests
{
    [Theory]
    [InlineData("- item", "", "", "- ", "item")]
    [InlineData("  1. item", "  ", "", "1. ", "item")]
    [InlineData("> quoted", "", "> ", "", "quoted")]
    [InlineData("> - item", "", "> ", "- ", "item")]
    [InlineData(">> deep", "", ">> ", "", "deep")]
    [InlineData("plain", "", "", "", "plain")]
    [InlineData("3.no space", "", "", "", "3.no space")]
    [InlineData("-nospace", "", "", "", "-nospace")]
    public void Splits_a_line_into_prefix_and_content(
        string line, string indent, string quote, string marker, string content)
    {
        var parsed = LineParser.Parse(line);

        Assert.Equal(indent, parsed.Indent);
        Assert.Equal(quote, parsed.QuotePrefix);
        Assert.Equal(marker, parsed.ListMarker);
        Assert.Equal(content, parsed.Content);
    }

    [Fact]
    public void Recognises_task_boxes_only_after_a_list_marker()
    {
        Assert.Equal("[x] ", LineParser.Parse("- [x] done").TaskBox);
        Assert.Equal(string.Empty, LineParser.Parse("[x] not a list").TaskBox);
    }

    [Fact]
    public void Reports_an_empty_item()
    {
        Assert.True(LineParser.Parse("- ").IsEmptyItem);
        Assert.True(LineParser.Parse("> ").IsEmptyItem);
        Assert.False(LineParser.Parse("- x").IsEmptyItem);
        Assert.False(LineParser.Parse("").IsEmptyItem);
    }
}
