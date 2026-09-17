using MarkdownPad.Core.Text;
using Xunit;

namespace MarkdownPad.Tests;

public class NewLineTests
{
    [Theory]
    [InlineData("a\r\nb\r\nc", NewLineStyle.Crlf)]
    [InlineData("a\nb\nc", NewLineStyle.Lf)]
    [InlineData("a\rb\rc", NewLineStyle.Cr)]
    [InlineData("no line break", NewLineStyle.Crlf)]
    [InlineData("a\r\nb\r\nc\nd", NewLineStyle.Crlf)]
    public void Detects_dominant_style(string text, NewLineStyle expected)
    {
        Assert.Equal(expected, NewLineDetector.Detect(text));
    }

    [Fact]
    public void Normalizes_and_restores_line_endings()
    {
        const string crlf = "one\r\ntwo\r\nthree";

        string lf = NewLineDetector.NormalizeToLf(crlf);
        Assert.Equal("one\ntwo\nthree", lf);
        Assert.Equal(crlf, NewLineDetector.ConvertTo(lf, NewLineStyle.Crlf));
    }

    [Fact]
    public void Conversion_does_not_double_up_crlf()
    {
        Assert.Equal("a\r\nb", NewLineDetector.ConvertTo("a\r\nb", NewLineStyle.Crlf));
    }

    [Fact]
    public void Saving_preserves_the_original_line_ending()
    {
        var loaded = TextFileService.Load(System.Text.Encoding.UTF8.GetBytes("a\nb\nc"));
        Assert.Equal(NewLineStyle.Lf, loaded.Format.NewLine);

        byte[] written = TextFileService.Encode(loaded.Text, loaded.Format);
        Assert.Equal("a\nb\nc", System.Text.Encoding.UTF8.GetString(written));
    }
}
