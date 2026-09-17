using System.IO;
using Microsoft.Win32;

namespace MarkdownPad.Services;

public static class SystemThemeDetector
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>
    /// Reads the Windows "apps use dark mode" preference. Defaults to light when
    /// the value is missing or unreadable.
    /// </summary>
    public static bool IsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}
