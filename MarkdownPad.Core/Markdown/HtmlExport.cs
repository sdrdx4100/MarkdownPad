using System.Net;

namespace MarkdownPad.Core.Markdown;

/// <summary>
/// Wraps a rendered fragment into a self-contained HTML file. Unlike the
/// preview, the exported document keeps the author's original link targets and
/// inlines the stylesheet, so it stays readable when moved alongside its images.
/// </summary>
public static class HtmlExport
{
    public static string BuildStandaloneDocument(string title, string bodyHtml)
    {
        return Template
            .Replace("__TITLE__", WebUtility.HtmlEncode(title), StringComparison.Ordinal)
            .Replace("__STYLE__", PreviewAssets.StyleSheet, StringComparison.Ordinal)
            .Replace("__BODY__", bodyHtml, StringComparison.Ordinal);
    }

    private const string Template = @"<!DOCTYPE html>
<html lang=""ja"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<meta name=""color-scheme"" content=""light dark"">
<title>__TITLE__</title>
<style>
__STYLE__
</style>
</head>
<body>
<article>
__BODY__
</article>
</body>
</html>
";
}
