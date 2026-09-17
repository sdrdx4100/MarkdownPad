using MarkdownPad.Core.Settings;
using Xunit;

namespace MarkdownPad.Tests;

public class AppSettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mdpad-tests-" + Guid.NewGuid().ToString("N"));

    public AppSettingsTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Round_trips_through_json()
    {
        string path = Path.Combine(_dir, "settings.json");
        var settings = new AppSettings { WordWrap = false, PreviewTheme = PreviewTheme.Dark, EditorRatio = 0.35 };
        settings.AddRecentFile(@"C:\a.md");
        settings.Save(path);

        var loaded = AppSettings.Load(path);

        Assert.False(loaded.WordWrap);
        Assert.Equal(PreviewTheme.Dark, loaded.PreviewTheme);
        Assert.Equal(0.35, loaded.EditorRatio, 3);
        Assert.Equal(@"C:\a.md", loaded.RecentFiles.Single());
    }

    [Fact]
    public void Corrupt_file_falls_back_to_defaults_instead_of_throwing()
    {
        string path = Path.Combine(_dir, "broken.json");
        File.WriteAllText(path, "{ not json");

        Assert.True(AppSettings.Load(path).WordWrap);
    }

    [Fact]
    public void Recent_files_are_deduplicated_most_recent_first_and_capped()
    {
        var settings = new AppSettings();

        for (int i = 0; i < AppSettings.MaxRecentFiles + 5; i++)
        {
            settings.AddRecentFile($"file{i}.md");
        }
        settings.AddRecentFile("FILE14.MD"); // same file, different casing

        Assert.Equal(AppSettings.MaxRecentFiles, settings.RecentFiles.Count);
        Assert.Equal("FILE14.MD", settings.RecentFiles[0]);
        Assert.Single(settings.RecentFiles, p => p.Equals("FILE14.MD", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Out_of_range_values_are_clamped()
    {
        var settings = new AppSettings { WindowWidth = 10, EditorRatio = 5, EditorFontSize = 900 }.Sanitized();

        Assert.Equal(600, settings.WindowWidth);
        Assert.Equal(0.9, settings.EditorRatio, 3);
        Assert.Equal(48, settings.EditorFontSize, 3);
    }

    [Fact]
    public void Recovery_snapshot_survives_a_round_trip_and_is_cleared_on_demand()
    {
        var store = new RecoveryStore(_dir);
        Assert.Null(store.Read());

        string original = Path.Combine(_dir, "a.md");
        store.Write(original, "unsaved work");
        var snapshot = store.Read();

        Assert.NotNull(snapshot);
        Assert.Equal("unsaved work", snapshot!.Text);
        Assert.Equal("a.md", snapshot.DisplayName);
        Assert.Equal(original, snapshot.OriginalPath);

        store.Clear();
        Assert.Null(store.Read());
    }
}
