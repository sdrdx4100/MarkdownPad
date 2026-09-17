using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace MarkdownPad.Core.Markdown;

public sealed record MarkdownRenderOptions
{
    /// <summary>Directory relative links are resolved against; null when unsaved.</summary>
    public string? DocumentDirectory { get; init; }

    /// <summary>Maps local paths onto URLs the preview may load.</summary>
    public IResourceUrlMapper? ResourceMapper { get; init; }

    /// <summary>
    /// When false, raw HTML embedded in the Markdown is escaped instead of passed
    /// through. The preview's Content-Security-Policy already blocks scripts, so
    /// this is a second, opt-in layer for untrusted documents.
    /// </summary>
    public bool AllowRawHtml { get; init; } = true;
}

/// <summary>
/// Converts Markdown to the HTML fragment shown in the preview pane.
/// Top-level blocks carry a <c>data-source-line</c> attribute so the preview can
/// be scrolled in step with the editor.
/// </summary>
public sealed class MarkdownRenderer
{
    public const string SourceLineAttribute = "data-source-line";

    private readonly MarkdownPipeline _htmlPipeline;
    private readonly MarkdownPipeline _noHtmlPipeline;

    public MarkdownRenderer()
    {
        // UseAdvancedExtensions already includes pipe/grid tables, task lists,
        // emphasis extras and auto links, so they are not listed again here.
        _htmlPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseSoftlineBreakAsHardlineBreak()
            .UsePreciseSourceLocation()
            .Build();

        _noHtmlPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseSoftlineBreakAsHardlineBreak()
            .UsePreciseSourceLocation()
            .DisableHtml()
            .Build();
    }

    public string Render(string markdown, MarkdownRenderOptions? options = null)
    {
        options ??= new MarkdownRenderOptions();
        var pipeline = options.AllowRawHtml ? _htmlPipeline : _noHtmlPipeline;

        var document = Markdig.Markdown.Parse(markdown, pipeline);
        AnnotateSourceLines(document);

        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer)
        {
            LinkRewriter = url => LinkUrlRewriter.Rewrite(url, options.DocumentDirectory, options.ResourceMapper),
        };

        pipeline.Setup(renderer);
        renderer.Render(document);
        writer.Flush();

        return writer.ToString();
    }

    /// <summary>
    /// Tags every top-level block with the editor line it came from. Only the top
    /// level is annotated: that is enough to scroll to, and it keeps the emitted
    /// HTML small.
    /// </summary>
    private static void AnnotateSourceLines(MarkdownDocument document)
    {
        foreach (var block in document)
        {
            if (block.Line < 0) continue;

            var attributes = block.GetAttributes();
            attributes.AddPropertyIfNotExist(SourceLineAttribute, (block.Line + 1).ToString());
        }
    }
}
