namespace MarkdownPad.Core.Images;

public static class RelativePath
{
    /// <summary>
    /// Expresses <paramref name="targetPath"/> relative to <paramref name="baseDirectory"/>
    /// using forward slashes, which is what a Markdown link needs. Falls back to
    /// the absolute path when the two live on different roots.
    /// </summary>
    public static string MakeMarkdownPath(string baseDirectory, string targetPath)
    {
        if (string.IsNullOrEmpty(baseDirectory)) return ToForwardSlashes(targetPath);

        try
        {
            string relative = Path.GetRelativePath(baseDirectory, targetPath);

            // GetRelativePath returns the input unchanged when there is no shared
            // root (different drives), in which case the absolute path is correct.
            return ToForwardSlashes(relative);
        }
        catch (Exception e) when (e is ArgumentException or PathTooLongException)
        {
            return ToForwardSlashes(targetPath);
        }
    }

    private static string ToForwardSlashes(string path) => path.Replace('\\', '/');
}
