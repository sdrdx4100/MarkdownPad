using System.IO;
using System.Windows.Media.Imaging;
using MarkdownPad.Core;
using MarkdownPad.Core.Images;

namespace MarkdownPad.Services;

/// <summary>Where a pasted image was written, and how to reference it from Markdown.</summary>
public sealed record SavedImage(string FullPath, string MarkdownPath, string FileName);

public sealed class ImageInsertionService
{
    /// <summary>
    /// Saves a clipboard bitmap next to the document, in an <c>images</c> folder.
    /// Falls back to the user's document library while the file is still unsaved.
    /// </summary>
    public SavedImage SaveClipboardImage(BitmapSource image, string? currentFilePath)
    {
        string targetDirectory = GetImageDirectory(currentFilePath);
        Directory.CreateDirectory(targetDirectory);

        string stem = ImageFiles.CreateTimestampStem(DateTime.Now);
        string fileName = ImageFiles.CreateUniqueFileName(
            stem, ".png", name => File.Exists(Path.Combine(targetDirectory, name)));

        string fullPath = Path.Combine(targetDirectory, fileName);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));

        using (var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write))
        {
            encoder.Save(stream);
        }

        return new SavedImage(fullPath, BuildMarkdownPath(fullPath, currentFilePath), fileName);
    }

    /// <summary>
    /// Copies an external image into the document's <c>images</c> folder,
    /// keeping its name unless that would overwrite something.
    /// </summary>
    public string CopyIntoLibrary(string sourcePath, string? currentFilePath)
    {
        string targetDirectory = GetImageDirectory(currentFilePath);
        Directory.CreateDirectory(targetDirectory);

        string stem = Path.GetFileNameWithoutExtension(sourcePath);
        string extension = Path.GetExtension(sourcePath);

        string fileName = ImageFiles.CreateUniqueFileName(
            stem, extension, name => File.Exists(Path.Combine(targetDirectory, name)));

        string fullPath = Path.Combine(targetDirectory, fileName);
        File.Copy(sourcePath, fullPath);

        return fullPath;
    }

    /// <summary>Markdown reference for an image that already exists on disk.</summary>
    public string BuildMarkdownPath(string imagePath, string? currentFilePath)
    {
        string? documentDirectory = string.IsNullOrEmpty(currentFilePath)
            ? null
            : Path.GetDirectoryName(currentFilePath);

        return string.IsNullOrEmpty(documentDirectory)
            ? imagePath.Replace('\\', '/')
            : RelativePath.MakeMarkdownPath(documentDirectory, imagePath);
    }

    private static string GetImageDirectory(string? currentFilePath)
    {
        string? documentDirectory = string.IsNullOrEmpty(currentFilePath)
            ? null
            : Path.GetDirectoryName(currentFilePath);

        return string.IsNullOrEmpty(documentDirectory)
            ? AppPaths.DefaultImageLibrary
            : Path.Combine(documentDirectory, "images");
    }
}
