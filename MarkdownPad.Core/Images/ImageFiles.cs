namespace MarkdownPad.Core.Images;

public static class ImageFiles
{
    private static readonly string[] Extensions =
        { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg", ".avif" };

    public static bool IsImageFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return Extensions.Contains(ext);
    }

    /// <summary>
    /// Builds a file name that does not collide with anything already in the
    /// target directory. The previous implementation used second precision only,
    /// so two screenshots pasted within the same second overwrote each other.
    /// </summary>
    /// <param name="exists">Existence probe; injected so this stays testable.</param>
    public static string CreateUniqueFileName(
        string baseName,
        string extension,
        Func<string, bool> exists,
        int maxAttempts = 1000)
    {
        if (!extension.StartsWith('.')) extension = "." + extension;

        string candidate = baseName + extension;
        if (!exists(candidate)) return candidate;

        for (int i = 2; i <= maxAttempts; i++)
        {
            candidate = $"{baseName}_{i}{extension}";
            if (!exists(candidate)) return candidate;
        }

        // Astronomically unlikely, but never return a name known to collide.
        return $"{baseName}_{Guid.NewGuid():N}{extension}";
    }

    /// <summary>Time-based stem for pasted screenshots, to millisecond precision.</summary>
    public static string CreateTimestampStem(DateTime timestamp, string prefix = "screenshot")
        => $"{prefix}_{timestamp:yyyyMMdd_HHmmssfff}";
}
