namespace MarkdownPad.Core;

/// <summary>
/// Every location the application writes to. All of them sit under the user's
/// profile: writing next to the executable breaks as soon as the app is
/// installed somewhere read-only such as Program Files, which is exactly what
/// happens to WebView2's default user-data folder.
/// </summary>
public static class AppPaths
{
    public const string AppName = "MarkdownPad";

    public static string RoamingRoot { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);

    public static string LocalRoot { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);

    /// <summary>WebView2's user data folder. Must be writable by the current user.</summary>
    public static string WebViewUserData => Path.Combine(LocalRoot, "WebView2");

    /// <summary>Where the static preview shell (html/css/js) is materialised.</summary>
    public static string PreviewAssets => Path.Combine(LocalRoot, "preview");

    /// <summary>Crash-recovery snapshots of unsaved buffers.</summary>
    public static string Backups => Path.Combine(LocalRoot, "backup");

    public static string SettingsFile => Path.Combine(RoamingRoot, "settings.json");

    /// <summary>Image folder used while the document has never been saved.</summary>
    public static string DefaultImageLibrary => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), AppName, "images");

    public static string DefaultDocumentFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), AppName);

    public static void EnsureDirectory(string path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            Directory.CreateDirectory(path);
        }
    }
}
