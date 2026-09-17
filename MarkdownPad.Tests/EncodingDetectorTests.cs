using System.Text;
using MarkdownPad.Core.Text;
using Xunit;

namespace MarkdownPad.Tests;

public class EncodingDetectorTests
{
    public EncodingDetectorTests() => TextEncodings.EnsureProviderRegistered();

    [Fact]
    public void Detects_utf8_with_bom()
    {
        byte[] bytes = TextEncodings.Utf8Bom.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes("こんにちは")).ToArray();

        var detected = EncodingDetector.Detect(bytes);

        Assert.True(detected.HasBom);
        Assert.Equal("UTF-8 (BOM付き)", detected.DisplayName);
    }

    [Fact]
    public void Detects_utf8_without_bom()
    {
        var detected = EncodingDetector.Detect(Encoding.UTF8.GetBytes("日本語のテキスト"));

        Assert.False(detected.HasBom);
        Assert.Equal("UTF-8", detected.DisplayName);
    }

    [Fact]
    public void Detects_shift_jis()
    {
        var sjis = TextEncodings.ShiftJis;
        Assert.NotNull(sjis);

        byte[] bytes = sjis!.GetBytes("日本語のテキストです。これは Shift_JIS です。");
        var detected = EncodingDetector.Detect(bytes);

        Assert.Equal("Shift_JIS", detected.DisplayName);
        Assert.Equal(TextEncodings.ShiftJisCodePage, detected.Encoding.CodePage);
    }

    [Fact]
    public void Shift_jis_round_trips_without_mojibake()
    {
        var sjis = TextEncodings.ShiftJis!;
        const string original = "# 見出し\n\n日本語の本文です。";

        var loaded = TextFileService.Load(sjis.GetBytes(original.Replace("\n", "\r\n")));

        Assert.Equal("Shift_JIS", loaded.Format.EncodingDisplayName);
        Assert.Equal(original, loaded.Text);

        byte[] written = TextFileService.Encode(loaded.Text, loaded.Format);
        Assert.Equal(sjis.GetBytes(original.Replace("\n", "\r\n")), written);
    }

    [Fact]
    public void Detects_utf16_le_with_bom()
    {
        byte[] bytes = new UnicodeEncoding(false, true).GetPreamble()
            .Concat(Encoding.Unicode.GetBytes("テスト")).ToArray();

        var detected = EncodingDetector.Detect(bytes);

        Assert.Equal("UTF-16 LE", detected.DisplayName);
        Assert.True(detected.HasBom);
    }

    [Fact]
    public void Empty_file_is_utf8()
    {
        var detected = EncodingDetector.Detect(Array.Empty<byte>());
        Assert.Equal("UTF-8", detected.DisplayName);
    }

    [Theory]
    [InlineData(new byte[] { 0xC0, 0x80 })]           // overlong
    [InlineData(new byte[] { 0xED, 0xA0, 0x80 })]     // lone surrogate
    [InlineData(new byte[] { 0xE3, 0x81 })]           // truncated
    [InlineData(new byte[] { 0xFF, 0xFF, 0xFF })]     // never valid
    public void Rejects_invalid_utf8(byte[] bytes)
    {
        Assert.False(EncodingDetector.IsValidUtf8(bytes));
    }

    [Fact]
    public void Reports_characters_that_the_target_encoding_cannot_represent()
    {
        var format = new TextDocumentFormat(TextEncodings.ShiftJis!, false, NewLineStyle.Lf, "Shift_JIS");

        Assert.True(TextFileService.TryFindUnencodableText("絵文字 🎉 入り", format, out string lost));
        Assert.Equal("🎉", lost);

        Assert.False(TextFileService.TryFindUnencodableText("通常の日本語", format, out _));
    }
}
