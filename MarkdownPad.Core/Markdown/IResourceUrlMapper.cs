namespace MarkdownPad.Core.Markdown;

/// <summary>
/// Turns an absolute path on disk into a URL the preview browser is allowed to
/// load. Implemented in the WPF layer on top of WebView2 virtual host mappings;
/// abstracted here so link rewriting stays testable.
/// </summary>
public interface IResourceUrlMapper
{
    /// <summary>
    /// Returns a loadable URL for <paramref name="absolutePath"/>, or <c>null</c>
    /// when the location cannot be exposed to the preview.
    /// </summary>
    string? MapLocalPath(string absolutePath);
}
