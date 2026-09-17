using MarkdownPad.Core.Text;
using Xunit;

namespace MarkdownPad.Tests;

public class LineIndexTests
{
    [Fact]
    public void Reports_one_based_line_and_column()
    {
        var index = new LineIndex();
        index.Rebuild("one\ntwo\nthree");

        Assert.Equal((1, 1), index.GetLineColumn(0));
        Assert.Equal((1, 4), index.GetLineColumn(3));
        Assert.Equal((2, 1), index.GetLineColumn(4));
        Assert.Equal((3, 6), index.GetLineColumn(13));
    }

    [Fact]
    public void Handles_empty_text_and_out_of_range_offsets()
    {
        var index = new LineIndex();
        index.Rebuild(string.Empty);

        Assert.Equal((1, 1), index.GetLineColumn(0));
        Assert.Equal((1, 1), index.GetLineColumn(50));
        Assert.Equal((1, 1), index.GetLineColumn(-5));
    }

    [Fact]
    public void Counts_trailing_newline_as_an_extra_line()
    {
        var index = new LineIndex();
        index.Rebuild("a\n");

        Assert.Equal(2, index.LineCount);
        Assert.Equal((2, 1), index.GetLineColumn(2));
    }

    [Fact]
    public void Line_starts_match_a_naive_scan()
    {
        const string text = "alpha\nbravo\n\ncharlie\n";
        var index = new LineIndex();
        index.Rebuild(text);

        for (int offset = 0; offset <= text.Length; offset++)
        {
            Assert.Equal(LineIndex.Compute(text, offset), index.GetLineColumn(offset));
        }
    }
}
