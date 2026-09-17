using System.Text.Json;
using System.Text.Json.Serialization;
using MarkdownPad.Core.Text;

namespace MarkdownPad.Core.Settings;

/// <summary>An unsaved buffer written to disk so a crash does not lose it.</summary>
public sealed class RecoverySnapshot
{
    public string? OriginalPath { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime SavedAtUtc { get; set; }

    [JsonIgnore]
    public string DisplayName =>
        string.IsNullOrEmpty(OriginalPath) ? "無題" : Path.GetFileName(OriginalPath);
}

/// <summary>
/// Periodic snapshots of the editor buffer. The snapshot is deleted on a clean
/// save or a clean exit, so a snapshot found at start-up means the previous run
/// ended unexpectedly with unsaved work.
/// </summary>
public sealed class RecoveryStore
{
    private const string SnapshotFileName = "recovery.json";

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    private readonly string _directory;

    public RecoveryStore(string directory) => _directory = directory;

    public string SnapshotPath => Path.Combine(_directory, SnapshotFileName);

    public void Write(string? originalPath, string text)
    {
        try
        {
            AppPaths.EnsureDirectory(_directory);

            var snapshot = new RecoverySnapshot
            {
                OriginalPath = originalPath,
                Text = text,
                SavedAtUtc = DateTime.UtcNow,
            };

            // Write to a temporary file first so an interrupted write cannot
            // destroy the previous, still-valid snapshot.
            string temporary = SnapshotPath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(snapshot, SerializerOptions), TextEncodings.Utf8NoBom);
            File.Move(temporary, SnapshotPath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // A failed snapshot must never interrupt editing.
        }
    }

    public RecoverySnapshot? Read()
    {
        try
        {
            if (!File.Exists(SnapshotPath)) return null;

            var snapshot = JsonSerializer.Deserialize<RecoverySnapshot>(
                File.ReadAllText(SnapshotPath), SerializerOptions);

            return string.IsNullOrEmpty(snapshot?.Text) ? null : snapshot;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(SnapshotPath)) File.Delete(SnapshotPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Nothing useful to do; a stale snapshot only costs one prompt.
        }
    }
}
