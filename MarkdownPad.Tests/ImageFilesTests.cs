using MarkdownPad.Core.Images;
using Xunit;

namespace MarkdownPad.Tests;

public class ImageFilesTests
{
    [Fact]
    public void Unique_name_is_returned_as_is_when_nothing_collides()
    {
        Assert.Equal("shot.png", ImageFiles.CreateUniqueFileName("shot", ".png", _ => false));
    }

    [Fact]
    public void Collisions_get_a_numeric_suffix_instead_of_overwriting()
    {
        var taken = new HashSet<string> { "shot.png", "shot_2.png" };

        Assert.Equal("shot_3.png", ImageFiles.CreateUniqueFileName("shot", ".png", taken.Contains));
    }

    [Fact]
    public void Timestamp_stem_has_millisecond_precision()
    {
        var at = new DateTime(2026, 9, 17, 21, 32, 5, 123);

        Assert.Equal("screenshot_20260917_213205123", ImageFiles.CreateTimestampStem(at));
    }

    [Theory]
    [InlineData("a.PNG", true)]
    [InlineData("a.webp", true)]
    [InlineData("a.md", false)]
    [InlineData("a", false)]
    [InlineData("", false)]
    public void Recognises_image_extensions(string path, bool expected)
    {
        Assert.Equal(expected, ImageFiles.IsImageFile(path));
    }

    [Fact]
    public void Markdown_paths_use_forward_slashes_relative_to_the_document()
    {
        string baseDir = OperatingSystem.IsWindows() ? @"C:\notes" : "/notes";
        string target = Path.Combine(baseDir, "images", "shot.png");

        Assert.Equal("images/shot.png", RelativePath.MakeMarkdownPath(baseDir, target));
    }

    [Fact]
    public void Paths_outside_the_document_tree_still_produce_a_usable_reference()
    {
        string baseDir = OperatingSystem.IsWindows() ? @"C:\notes\sub" : "/notes/sub";
        string target = OperatingSystem.IsWindows() ? @"C:\notes\img.png" : "/notes/img.png";

        Assert.Equal("../img.png", RelativePath.MakeMarkdownPath(baseDir, target));
    }
}
