using MarkdownPad.Core.Markdown;
using Xunit;

namespace MarkdownPad.Tests;

file sealed class FakeMapper : IResourceUrlMapper
{
    public List<string> Requested { get; } = new();

    public string? MapLocalPath(string absolutePath)
    {
        Requested.Add(absolutePath);
        return "https://doc.local/" + Path.GetFileName(absolutePath);
    }
}

public class MarkdownRendererTests
{
    private static readonly string DocDir = OperatingSystem.IsWindows() ? @"C:\notes" : "/notes";

    [Fact]
    public void Relative_image_paths_are_mapped_onto_a_loadable_url()
    {
        var mapper = new FakeMapper();
        var renderer = new MarkdownRenderer();

        string html = renderer.Render("![shot](images/shot.png)", new MarkdownRenderOptions
        {
            DocumentDirectory = DocDir,
            ResourceMapper = mapper,
        });

        Assert.Contains("https://doc.local/shot.png", html);
        Assert.Equal(Path.Combine(DocDir, "images", "shot.png"), mapper.Requested.Single());
    }

    [Fact]
    public void Remote_and_data_urls_are_left_alone()
    {
        var renderer = new MarkdownRenderer();
        var mapper = new FakeMapper();

        string html = renderer.Render(
            "![a](https://example.com/a.png)\n\n![b](data:image/png;base64,AAAA)",
            new MarkdownRenderOptions { DocumentDirectory = DocDir, ResourceMapper = mapper });

        Assert.Contains("https://example.com/a.png", html);
        Assert.Contains("data:image/png;base64,AAAA", html);
        Assert.Empty(mapper.Requested);
    }

    [Fact]
    public void Top_level_blocks_carry_their_source_line()
    {
        string html = new MarkdownRenderer().Render("# Title\n\nParagraph\n");

        Assert.Contains(@"data-source-line=""1""", html);
        Assert.Contains(@"data-source-line=""3""", html);
    }

    [Fact]
    public void Raw_html_is_escaped_when_disabled()
    {
        var renderer = new MarkdownRenderer();
        const string markdown = "<script>alert(1)</script>";

        Assert.Contains("<script>", renderer.Render(markdown, new MarkdownRenderOptions { AllowRawHtml = true }));
        Assert.DoesNotContain("<script>", renderer.Render(markdown, new MarkdownRenderOptions { AllowRawHtml = false }));
    }

    [Fact]
    public void Tables_and_task_lists_render_without_extra_extensions()
    {
        string html = new MarkdownRenderer().Render("| a | b |\n|---|---|\n| 1 | 2 |\n\n- [x] done\n");

        Assert.Contains("<table", html);
        Assert.Contains("<th>a</th>", html);
        Assert.Contains("type=\"checkbox\"", html);
    }

    [Fact]
    public void Preview_shell_pins_a_nonce_and_forbids_injected_script()
    {
        string shell = PreviewAssets.BuildShellHtml("abc123", "https://app.local");

        Assert.Contains("script-src 'nonce-abc123'", shell);
        Assert.Contains(@"<script nonce=""abc123""", shell);
        Assert.DoesNotContain("__NONCE__", shell);
        Assert.DoesNotContain("__ASSET_ORIGIN__", shell);
    }
}

public class LinkUrlRewriterTests
{
    private sealed class NullMapper : IResourceUrlMapper
    {
        public string? MapLocalPath(string absolutePath) => null;
    }

    [Theory]
    [InlineData("#section")]
    [InlineData("https://example.com/x")]
    [InlineData("mailto:a@example.com")]
    public void Passthrough_urls_are_unchanged(string url)
    {
        Assert.Equal(url, LinkUrlRewriter.Rewrite(url, "/docs", new NullMapper()));
    }

    [Fact]
    public void Unmappable_paths_fall_back_to_the_original_text()
    {
        Assert.Equal("images/a.png", LinkUrlRewriter.Rewrite("images/a.png", "/docs", new NullMapper()));
    }

    [Fact]
    public void Percent_encoded_paths_are_decoded_before_resolving()
    {
        var mapper = new FakeMapper();
        LinkUrlRewriter.Rewrite("images/my%20shot.png", OperatingSystem.IsWindows() ? @"C:\d" : "/d", mapper);

        Assert.EndsWith("my shot.png", mapper.Requested.Single());
    }

    [Fact]
    public void Query_and_fragment_are_stripped_from_local_paths()
    {
        var mapper = new FakeMapper();
        LinkUrlRewriter.Rewrite("a.png?v=2", OperatingSystem.IsWindows() ? @"C:\d" : "/d", mapper);

        Assert.EndsWith("a.png", mapper.Requested.Single());
    }

    [Fact]
    public void Relative_paths_without_a_document_directory_are_left_alone()
    {
        Assert.Equal("images/a.png", LinkUrlRewriter.Rewrite("images/a.png", null, new FakeMapper()));
    }
}
