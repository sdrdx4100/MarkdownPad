namespace MarkdownPad.Core.Markdown;

/// <summary>
/// Rewrites the URLs found in a Markdown document so the preview can actually
/// load them. Remote and data URLs pass through untouched; anything that points
/// at the local file system is resolved against the document's directory and
/// handed to an <see cref="IResourceUrlMapper"/>.
/// </summary>
/// <remarks>
/// This is the fix for local images never appearing in the preview: a document
/// shown with <c>NavigateToString</c> has an <c>about:blank</c> origin, so
/// neither <c>images/shot.png</c> nor <c>C:\dir\shot.png</c> can resolve.
/// </remarks>
public static class LinkUrlRewriter
{
    private static readonly string[] PassthroughSchemes =
        { "http", "https", "data", "mailto", "blob", "tel", "about" };

    public static string Rewrite(string url, string? documentDirectory, IResourceUrlMapper? mapper)
    {
        if (string.IsNullOrWhiteSpace(url)) return url;

        // In-page anchors are handled by the preview itself.
        if (url.StartsWith('#')) return url;

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            if (PassthroughSchemes.Contains(absolute.Scheme, StringComparer.OrdinalIgnoreCase))
            {
                return url;
            }

            if (absolute.IsFile)
            {
                return MapOrKeep(absolute.LocalPath, url, mapper);
            }

            // Protocol-relative ("//host/path") and unknown schemes are left alone.
            return url;
        }

        string localPath = ToLocalPath(url);
        if (localPath.Length == 0) return url;

        if (!Path.IsPathRooted(localPath))
        {
            if (string.IsNullOrEmpty(documentDirectory)) return url;
            localPath = Path.Combine(documentDirectory, localPath);
        }

        try
        {
            localPath = Path.GetFullPath(localPath);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return url;
        }

        return MapOrKeep(localPath, url, mapper);
    }

    private static string MapOrKeep(string localPath, string originalUrl, IResourceUrlMapper? mapper)
        => mapper?.MapLocalPath(localPath) ?? originalUrl;

    /// <summary>
    /// Converts a Markdown destination into a file system path: percent-decodes it,
    /// strips any query or fragment, and normalizes separators.
    /// </summary>
    private static string ToLocalPath(string url)
    {
        int cut = url.IndexOfAny(new[] { '?', '#' });
        string path = cut >= 0 ? url[..cut] : url;

        try
        {
            path = Uri.UnescapeDataString(path);
        }
        catch (UriFormatException)
        {
            // Keep the raw text when it is not valid percent-encoding.
        }

        return path.Replace('/', Path.DirectorySeparatorChar).Trim();
    }
}
