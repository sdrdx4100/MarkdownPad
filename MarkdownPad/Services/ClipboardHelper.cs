using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MarkdownPad.Services;

/// <summary>
/// Clipboard access that never throws at the caller.
/// </summary>
/// <remarks>
/// The Win32 clipboard is a shared, lockable resource: when another process holds
/// it open, every call raises <see cref="COMException"/> (CLIPBRD_E_CANT_OPEN).
/// Each accessor therefore retries briefly and then gives up quietly.
/// </remarks>
public static class ClipboardHelper
{
    private const int RetryCount = 5;
    private const int RetryDelayMs = 40;

    public static BitmapSource? TryGetImage() => Try(Clipboard.GetImage);

    public static string? TryGetText() => Try(Clipboard.GetText);

    public static IReadOnlyList<string> TryGetFileDropList()
    {
        var list = Try(() => Clipboard.GetFileDropList());
        if (list is null) return Array.Empty<string>();

        return list.Cast<string?>().Where(p => !string.IsNullOrEmpty(p)).Select(p => p!).ToArray();
    }

    public static bool TrySetText(string text)
    {
        for (int attempt = 0; attempt < RetryCount; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch (Exception e) when (e is COMException or OutOfMemoryException)
            {
                Thread.Sleep(RetryDelayMs);
            }
        }

        return false;
    }

    private static T? Try<T>(Func<T> accessor) where T : class
    {
        for (int attempt = 0; attempt < RetryCount; attempt++)
        {
            try
            {
                return accessor();
            }
            catch (Exception e) when (e is COMException or OutOfMemoryException or NotSupportedException)
            {
                Thread.Sleep(RetryDelayMs);
            }
        }

        return null;
    }
}
