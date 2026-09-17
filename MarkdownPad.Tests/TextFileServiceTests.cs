using System.Text;
using MarkdownPad.Core.Markdown;
using MarkdownPad.Core.Text;
using Xunit;

namespace MarkdownPad.Tests;

public class TextFileServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mdpad-io-" + Guid.NewGuid().ToString("N"));

    public TextFileServiceTests()
    {
        TextEncodings.EnsureProviderRegistered();
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void A_shift_jis_file_survives_open_and_save_byte_for_byte()
    {
        var sjis = TextEncodings.ShiftJis!;
        string path = Path.Combine(_dir, "sjis.md");
        byte[] original = sjis.GetBytes("# 日本語\r\n\r\n本文です。\r\n");
        File.WriteAllBytes(path, original);

        var loaded = TextFileService.Load(path);
        TextFileService.Save(path, loaded.Text, loaded.Format);

        Assert.Equal(original, File.ReadAllBytes(path));
    }

    [Fact]
    public void A_utf8_bom_file_keeps_its_bom_on_save()
    {
        string path = Path.Combine(_dir, "bom.md");
        File.WriteAllText(path, "内容", TextEncodings.Utf8Bom);

        var loaded = TextFileService.Load(path);
        Assert.True(loaded.Format.HasBom);

        TextFileService.Save(path, loaded.Text, loaded.Format);
        byte[] written = File.ReadAllBytes(path);

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, written.Take(3));
    }

    [Fact]
    public void Editor_text_is_always_lf_normalised_whatever_the_file_used()
    {
        string path = Path.Combine(_dir, "crlf.md");
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes("a\r\nb\r\nc"));

        var loaded = TextFileService.Load(path);

        Assert.DoesNotContain('\r', loaded.Text);
        Assert.Equal(NewLineStyle.Crlf, loaded.Format.NewLine);
    }

    [Fact]
    public void Overwriting_an_existing_file_leaves_no_temporary_behind()
    {
        string path = Path.Combine(_dir, "atomic.md");
        File.WriteAllText(path, "old content");

        var format = new TextDocumentFormat(TextEncodings.Utf8NoBom, false, NewLineStyle.Lf, "UTF-8");
        TextFileService.Save(path, "new content", format);

        Assert.Equal("new content", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(_dir, "*.mdpad-tmp"));
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public void Saving_to_a_new_path_creates_the_file()
    {
        string path = Path.Combine(_dir, "fresh.md");
        var format = new TextDocumentFormat(TextEncodings.Utf8NoBom, false, NewLineStyle.Lf, "UTF-8");

        TextFileService.Save(path, "hello", format);

        Assert.Equal("hello", File.ReadAllText(path));
    }

    [Fact]
    public void File_stamp_tracks_external_modification()
    {
        string path = Path.Combine(_dir, "watched.md");
        File.WriteAllText(path, "one");

        var before = TextFileService.TryReadStamp(path);
        Assert.NotNull(before);

        File.WriteAllText(path, "one and two");
        var after = TextFileService.TryReadStamp(path);

        Assert.NotEqual(before, after);
        Assert.Null(TextFileService.TryReadStamp(Path.Combine(_dir, "missing.md")));
    }

    [Fact]
    public void Status_text_reports_both_encoding_and_line_ending()
    {
        var format = new TextDocumentFormat(TextEncodings.Utf8NoBom, false, NewLineStyle.Lf, "UTF-8");
        Assert.Equal("UTF-8 / LF", format.StatusText);
    }
}

public class HtmlExportTests
{
    [Fact]
    public void Exported_document_is_self_contained()
    {
        string body = new MarkdownRenderer().Render("# 見出し\n\n本文");
        string html = HtmlExport.BuildStandaloneDocument("メモ.md", body);

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("<title>メモ.md</title>", html);
        Assert.Contains("--bg:", html);            // stylesheet inlined
        Assert.Contains("<h1", html);
        Assert.DoesNotContain("__BODY__", html);
        Assert.DoesNotContain("__STYLE__", html);
    }

    [Fact]
    public void Title_is_html_encoded()
    {
        string html = HtmlExport.BuildStandaloneDocument("<script>", "");
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<title><script>", html);
    }

    [Fact]
    public void Guide_text_is_valid_markdown_and_renders()
    {
        string html = new MarkdownRenderer().Render(MarkdownGuide.Text);

        Assert.Contains("<h1", html);
        Assert.Contains("<table", html);
        Assert.Contains("type=\"checkbox\"", html);
    }
}
