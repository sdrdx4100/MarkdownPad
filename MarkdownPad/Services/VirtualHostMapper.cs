using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using MarkdownPad.Core.Markdown;

namespace MarkdownPad.Services;

/// <summary>
/// Exposes local folders to the preview through WebView2 virtual host mappings,
/// which is what lets the preview load images from disk at all. A page shown
/// with <c>NavigateToString</c> has an <c>about:blank</c> origin and can resolve
/// neither relative paths nor <c>file://</c> URLs.
/// </summary>
public sealed class VirtualHostMapper : IResourceUrlMapper
{
    public const string AppHost = "markdownpad-app.local";
    public const string DocumentHost = "markdownpad-doc.local";
    private const string ExtraHostSuffix = ".markdownpad-ext.local";

    /// <summary>
    /// Upper bound on folders mapped for absolute paths that sit outside the
    /// document's own directory. Each mapping widens what the preview can read,
    /// so the number is deliberately small.
    /// </summary>
    private const int MaxExtraMappings = 16;

    private readonly Dictionary<string, string> _extraHosts = new(StringComparer.OrdinalIgnoreCase);

    private CoreWebView2? _core;
    private string? _documentDirectory;

    public void Attach(CoreWebView2 core, string appAssetFolder)
    {
        _core = core;
        core.SetVirtualHostNameToFolderMapping(AppHost, appAssetFolder, CoreWebView2HostResourceAccessKind.Allow);

        // A document opened while WebView2 was still starting recorded its
        // directory but had nothing to map it on. Apply it now, or its images
        // would stay broken for as long as that file is open.
        ApplyDocumentMapping(_documentDirectory);
    }

    public bool IsKnownHost(string host)
        => host.Equals(AppHost, StringComparison.OrdinalIgnoreCase)
           || host.Equals(DocumentHost, StringComparison.OrdinalIgnoreCase)
           || host.EndsWith(ExtraHostSuffix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Points the document host at <paramref name="directory"/>. Called whenever
    /// the open file changes so relative links keep resolving.
    /// </summary>
    public void SetDocumentDirectory(string? directory)
    {
        if (string.Equals(_documentDirectory, directory, StringComparison.OrdinalIgnoreCase)) return;

        _documentDirectory = directory;
        ApplyDocumentMapping(directory);
    }

    private void ApplyDocumentMapping(string? directory)
    {
        if (_core is null) return;

        try
        {
            _core.ClearVirtualHostNameToFolderMapping(DocumentHost);

            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                _core.SetVirtualHostNameToFolderMapping(DocumentHost, directory, CoreWebView2HostResourceAccessKind.Allow);
            }
        }
        catch (Exception e) when (e is COMException or ObjectDisposedException or InvalidOperationException)
        {
            // The preview simply keeps its previous mapping.
        }
    }

    public string? MapLocalPath(string absolutePath)
    {
        if (_core is null || string.IsNullOrEmpty(absolutePath)) return null;

        string? directory = Path.GetDirectoryName(absolutePath);
        if (string.IsNullOrEmpty(directory)) return null;

        if (!string.IsNullOrEmpty(_documentDirectory) && IsUnder(_documentDirectory, absolutePath))
        {
            string relative = Path.GetRelativePath(_documentDirectory, absolutePath);
            return BuildUrl(DocumentHost, relative);
        }

        string? host = GetOrCreateExtraHost(directory);
        return host is null ? null : BuildUrl(host, Path.GetFileName(absolutePath));
    }

    /// <summary>Drops the ad-hoc mappings; called when a different document is opened.</summary>
    public void ResetExtraMappings()
    {
        if (_core is not null)
        {
            foreach (string host in _extraHosts.Values)
            {
                try
                {
                    _core.ClearVirtualHostNameToFolderMapping(host);
                }
                catch (Exception e) when (e is COMException or ObjectDisposedException or InvalidOperationException)
                {
                    // Nothing actionable; the mapping disappears with the process.
                }
            }
        }

        _extraHosts.Clear();
    }

    private string? GetOrCreateExtraHost(string directory)
    {
        if (_extraHosts.TryGetValue(directory, out string? existing)) return existing;
        if (_extraHosts.Count >= MaxExtraMappings || !Directory.Exists(directory)) return null;

        string host = $"d{_extraHosts.Count + 1}{ExtraHostSuffix}";

        try
        {
            _core!.SetVirtualHostNameToFolderMapping(host, directory, CoreWebView2HostResourceAccessKind.Allow);
        }
        catch (Exception e) when (e is COMException or ObjectDisposedException or InvalidOperationException)
        {
            return null;
        }

        _extraHosts[directory] = host;
        return host;
    }

    private static bool IsUnder(string directory, string path)
    {
        string relative = Path.GetRelativePath(directory, path);
        return !Path.IsPathRooted(relative) && !relative.StartsWith("..", StringComparison.Ordinal);
    }

    private static string BuildUrl(string host, string relativePath)
    {
        string encoded = string.Join('/', relativePath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString));

        return $"https://{host}/{encoded}";
    }
}
