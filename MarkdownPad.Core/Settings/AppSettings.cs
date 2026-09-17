using System.Text.Json;
using System.Text.Json.Serialization;

namespace MarkdownPad.Core.Settings;

public enum PreviewTheme
{
    Light,
    Dark,
    FollowSystem,
}

/// <summary>
/// User preferences persisted between runs. Loading never throws: a missing or
/// corrupt file falls back to defaults, because failing to start over a settings
/// file would be worse than losing the preferences.
/// </summary>
public sealed class AppSettings
{
    public const int MaxRecentFiles = 10;

    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 700;
    public bool WindowMaximized { get; set; }

    public bool PreviewVisible { get; set; } = true;
    public bool WordWrap { get; set; } = true;
    public bool ShowLineNumbers { get; set; } = true;
    public bool SyncScroll { get; set; } = true;
    public bool AllowRawHtml { get; set; } = true;

    public double EditorRatio { get; set; } = 0.5;
    public double EditorFontSize { get; set; } = 14;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PreviewTheme PreviewTheme { get; set; } = PreviewTheme.Light;

    public List<string> RecentFiles { get; set; } = new();

    /// <summary>
    /// Copy images dropped or pasted from elsewhere into the document's own
    /// folder, so a note does not break when the source file moves.
    /// </summary>
    public bool CopyImportedImages { get; set; } = true;

    public bool AutoSaveEnabled { get; set; } = true;
    public int AutoSaveIntervalSeconds { get; set; } = 30;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public void AddRecentFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentFiles.Insert(0, path);

        if (RecentFiles.Count > MaxRecentFiles)
        {
            RecentFiles.RemoveRange(MaxRecentFiles, RecentFiles.Count - MaxRecentFiles);
        }
    }

    public static AppSettings Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new AppSettings();

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions)?.Sanitized()
                   ?? new AppSettings();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(string path)
    {
        try
        {
            string? directory = Path.GetDirectoryName(path);
            AppPaths.EnsureDirectory(directory ?? string.Empty);

            File.WriteAllText(path, JsonSerializer.Serialize(this, SerializerOptions));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Preferences are not worth interrupting the user over.
        }
    }

    /// <summary>Clamps values so a hand-edited file cannot produce an unusable window.</summary>
    public AppSettings Sanitized()
    {
        WindowWidth = Math.Clamp(WindowWidth, 600, 10000);
        WindowHeight = Math.Clamp(WindowHeight, 400, 10000);
        EditorRatio = Math.Clamp(EditorRatio, 0.1, 0.9);
        EditorFontSize = Math.Clamp(EditorFontSize, 8, 48);
        AutoSaveIntervalSeconds = Math.Clamp(AutoSaveIntervalSeconds, 5, 3600);

        RecentFiles ??= new List<string>();
        if (RecentFiles.Count > MaxRecentFiles)
        {
            RecentFiles.RemoveRange(MaxRecentFiles, RecentFiles.Count - MaxRecentFiles);
        }

        return this;
    }
}
