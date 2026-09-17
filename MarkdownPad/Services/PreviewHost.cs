using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using MarkdownPad.Core;
using MarkdownPad.Core.Markdown;
using MarkdownPad.Core.Settings;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace MarkdownPad.Services;

/// <summary>
/// Owns the WebView2 that renders the preview.
/// </summary>
/// <remarks>
/// Two things here differ from the original implementation and matter a lot:
/// the browser's user-data folder lives under %LOCALAPPDATA% rather than beside
/// the executable (which fails outright under Program Files), and updates push
/// HTML into an already loaded page instead of re-navigating, so the preview no
/// longer jumps back to the top on every keystroke.
/// </remarks>
public sealed class PreviewHost : IDisposable
{
    private readonly WebView2 _webView;
    private readonly VirtualHostMapper _mapper = new();
    private readonly string _nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    private bool _shellReady;
    private string? _pendingHtml;
    private int? _pendingScrollLine;
    private PreviewTheme _theme = PreviewTheme.Light;

    public PreviewHost(WebView2 webView) => _webView = webView;

    public IResourceUrlMapper ResourceMapper => _mapper;

    public bool IsReady => _shellReady;

    /// <summary>Raised when initialisation fails, so the UI can explain why the pane is blank.</summary>
    public event EventHandler<string>? InitializationFailed;

    public async Task InitializeAsync()
    {
        try
        {
            AppPaths.EnsureDirectory(AppPaths.WebViewUserData);
            string assetFolder = WritePreviewAssets();

            // Explicit user-data folder: the default sits next to the .exe and is
            // unwritable for any per-machine installation.
            var environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: AppPaths.WebViewUserData);

            await _webView.EnsureCoreWebView2Async(environment);

            var core = _webView.CoreWebView2;
            if (core is null)
            {
                InitializationFailed?.Invoke(this, "WebView2 を初期化できませんでした。");
                return;
            }

            ConfigureSettings(core);
            _mapper.Attach(core, assetFolder);

            core.NavigationStarting += OnNavigationStarting;
            core.NewWindowRequested += OnNewWindowRequested;
            core.NavigationCompleted += OnNavigationCompleted;

            core.Navigate($"https://{VirtualHostMapper.AppHost}/{PreviewAssets.ShellFileName}");
        }
        catch (Exception e) when (e is WebView2RuntimeNotFoundException)
        {
            InitializationFailed?.Invoke(this,
                "WebView2 ランタイムが見つかりません。Microsoft Edge WebView2 Runtime をインストールしてください。");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            InitializationFailed?.Invoke(this, $"プレビューを初期化できませんでした: {e.Message}");
        }
    }

    public void SetDocumentDirectory(string? directory)
    {
        _mapper.ResetExtraMappings();
        _mapper.SetDocumentDirectory(directory);
    }

    /// <summary>Replaces the preview body, preserving the reader's scroll position.</summary>
    public void Render(string html)
    {
        if (!_shellReady)
        {
            _pendingHtml = html;
            return;
        }

        Invoke($"window.__mdpad.render({JsonSerializer.Serialize(html)});");
    }

    /// <summary>Scrolls the preview to the position matching an editor line.</summary>
    public void ScrollToLine(int line)
    {
        if (!_shellReady)
        {
            _pendingScrollLine = line;
            return;
        }

        Invoke($"window.__mdpad.scrollToLine({line});");
    }

    public void SetTheme(PreviewTheme theme)
    {
        _theme = theme;
        if (!_shellReady) return;

        Invoke($"window.__mdpad.setTheme({JsonSerializer.Serialize(ResolveTheme(theme))});");
    }

    /// <summary>Exports the current preview to PDF using the browser's own printer.</summary>
    public async Task<bool> PrintToPdfAsync(string path)
    {
        var core = _webView.CoreWebView2;
        if (core is null || !_shellReady) return false;

        return await core.PrintToPdfAsync(path);
    }

    /// <summary>Opens the browser's print dialog for the rendered preview.</summary>
    public void ShowPrintDialog()
    {
        try
        {
            _webView.CoreWebView2?.ShowPrintUI(CoreWebView2PrintDialogKind.Browser);
        }
        catch (Exception e) when (e is InvalidOperationException or ObjectDisposedException or COMException)
        {
            InitializationFailed?.Invoke(this, $"印刷ダイアログを開けませんでした: {e.Message}");
        }
    }

    private static void ConfigureSettings(CoreWebView2 core)
    {
        var settings = core.Settings;
        settings.AreDevToolsEnabled = false;
        settings.AreHostObjectsAllowed = false;
        settings.IsStatusBarEnabled = false;
        settings.IsSwipeNavigationEnabled = false;
        settings.IsZoomControlEnabled = true;
        settings.AreDefaultContextMenusEnabled = true;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
    }

    /// <summary>
    /// Materialises the shell, stylesheet and script under %LOCALAPPDATA% and
    /// returns the folder they live in. Rewritten on every run so the assets can
    /// never lag behind the application binary.
    /// </summary>
    private string WritePreviewAssets()
    {
        string folder = AppPaths.PreviewAssets;
        AppPaths.EnsureDirectory(folder);

        File.WriteAllText(
            Path.Combine(folder, PreviewAssets.ShellFileName),
            PreviewAssets.BuildShellHtml(_nonce, $"https://{VirtualHostMapper.AppHost}"));

        File.WriteAllText(Path.Combine(folder, PreviewAssets.StyleFileName), PreviewAssets.StyleSheet);
        File.WriteAllText(Path.Combine(folder, PreviewAssets.ScriptFileName), PreviewAssets.Script);

        return folder;
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            InitializationFailed?.Invoke(this, $"プレビューの読み込みに失敗しました ({e.WebErrorStatus}).");
            return;
        }

        _shellReady = true;
        SetTheme(_theme);

        if (_pendingHtml is not null)
        {
            Render(_pendingHtml);
            _pendingHtml = null;
        }

        if (_pendingScrollLine is int line)
        {
            ScrollToLine(line);
            _pendingScrollLine = null;
        }
    }

    /// <summary>
    /// Keeps the preview a preview: anything that is not our own shell opens in
    /// the user's browser instead of turning the pane into one.
    /// </summary>
    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri)) return;
        if (_mapper.IsKnownHost(uri.Host)) return;

        e.Cancel = true;
        OpenExternally(uri);
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;

        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri))
        {
            OpenExternally(uri);
        }
    }

    private static void OpenExternally(Uri uri)
    {
        // Only hand the shell schemes that cannot be used to launch a local file.
        if (uri.Scheme is not ("http" or "https" or "mailto")) return;

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // No default handler registered; nothing worth interrupting the user for.
        }
    }

    private static string ResolveTheme(PreviewTheme theme) => theme switch
    {
        PreviewTheme.Dark => "dark",
        PreviewTheme.FollowSystem => SystemThemeDetector.IsDarkMode() ? "dark" : "light",
        _ => "light",
    };

    private async void Invoke(string script)
    {
        try
        {
            var core = _webView.CoreWebView2;
            if (core is null) return;

            await core.ExecuteScriptAsync(script);
        }
        catch (Exception e) when (e is InvalidOperationException or ObjectDisposedException or COMException)
        {
            // The view was torn down mid-update; the next render will catch up.
        }
    }

    public void Dispose()
    {
        _mapper.ResetExtraMappings();
        _webView.Dispose();
    }
}
