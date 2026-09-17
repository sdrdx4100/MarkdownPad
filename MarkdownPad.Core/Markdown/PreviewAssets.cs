namespace MarkdownPad.Core.Markdown;

/// <summary>
/// The static shell the preview pane navigates to once at start-up. Subsequent
/// updates replace the document body through script instead of re-navigating,
/// which is what keeps the preview's scroll position stable while typing.
/// </summary>
public static class PreviewAssets
{
    public const string ShellFileName = "preview.html";
    public const string StyleFileName = "preview.css";
    public const string ScriptFileName = "preview.js";

    /// <summary>
    /// Builds the shell page. <paramref name="nonce"/> authorises only our own
    /// script: anything a Markdown document injects is refused by the CSP, which
    /// closes off both &lt;script&gt; tags and inline event handlers such as
    /// <c>onerror</c>.
    /// </summary>
    /// <param name="nonce">A fresh random value for this application run.</param>
    /// <param name="assetOrigin">Origin serving the css/js next to this page.</param>
    public static string BuildShellHtml(string nonce, string assetOrigin)
    {
        return ShellTemplate
            .Replace("__NONCE__", nonce, StringComparison.Ordinal)
            .Replace("__ASSET_ORIGIN__", assetOrigin, StringComparison.Ordinal);
    }

    private const string ShellTemplate = @"<!DOCTYPE html>
<html lang=""ja"">
<head>
<meta charset=""UTF-8"">
<meta http-equiv=""Content-Security-Policy"" content=""default-src 'none'; img-src http: https: data: blob:; media-src http: https: data: blob:; style-src 'unsafe-inline' __ASSET_ORIGIN__; font-src http: https: data:; script-src 'nonce-__NONCE__'; connect-src 'none'; frame-src 'none'; object-src 'none'; form-action 'none'; base-uri 'none'"">
<meta name=""color-scheme"" content=""light dark"">
<title>preview</title>
<link rel=""stylesheet"" href=""preview.css"">
</head>
<body>
<article id=""content""></article>
<script nonce=""__NONCE__"" src=""preview.js""></script>
</body>
</html>";

    public static string StyleSheet => StyleTemplate;

    public static string Script => ScriptTemplate;

    private const string StyleTemplate = @":root {
    color-scheme: light;
    --bg: #ffffff;
    --fg: #24292f;
    --muted: #6a737d;
    --border: #d8dee4;
    --subtle-bg: #f6f8fa;
    --link: #0969da;
    --mark: rgba(255, 213, 0, 0.45);
}

html[data-theme='dark'] {
    color-scheme: dark;
    --bg: #0d1117;
    --fg: #e6edf3;
    --muted: #9198a1;
    --border: #30363d;
    --subtle-bg: #161b22;
    --link: #4493f8;
    --mark: rgba(187, 128, 9, 0.5);
}

* { box-sizing: border-box; }

body {
    margin: 0;
    padding: 24px 28px 64px;
    background-color: var(--bg);
    color: var(--fg);
    font-family: 'Segoe UI', 'Yu Gothic UI', 'Meiryo', system-ui, sans-serif;
    font-size: 15px;
    line-height: 1.7;
    overflow-wrap: break-word;
}

h1, h2, h3, h4, h5, h6 {
    margin: 1.6em 0 0.6em;
    font-weight: 600;
    line-height: 1.3;
}

h1, h2 {
    border-bottom: 1px solid var(--border);
    padding-bottom: 0.3em;
}

h1 { font-size: 1.9em; }
h2 { font-size: 1.5em; }
h3 { font-size: 1.25em; }
h4 { font-size: 1.05em; }

p, ul, ol, blockquote, table, pre { margin: 0 0 1em; }

code {
    background-color: var(--subtle-bg);
    border-radius: 4px;
    padding: 0.15em 0.4em;
    font-family: 'Cascadia Mono', 'Consolas', 'BIZ UDGothic', monospace;
    font-size: 0.88em;
}

pre {
    background-color: var(--subtle-bg);
    border: 1px solid var(--border);
    border-radius: 6px;
    padding: 14px 16px;
    overflow: auto;
}

pre code {
    background: none;
    padding: 0;
    font-size: 0.86em;
}

blockquote {
    margin-left: 0;
    padding: 0 1em;
    color: var(--muted);
    border-left: 4px solid var(--border);
}

table {
    border-collapse: collapse;
    display: block;
    width: max-content;
    max-width: 100%;
    overflow: auto;
}

th, td {
    border: 1px solid var(--border);
    padding: 6px 13px;
}

th { background-color: var(--subtle-bg); font-weight: 600; }
tbody tr:nth-child(even) { background-color: var(--subtle-bg); }

img { max-width: 100%; height: auto; }

img.mdpad-broken {
    outline: 1px dashed #d1242f;
    padding: 4px;
    min-width: 24px;
    min-height: 24px;
}

a { color: var(--link); text-decoration: none; }
a:hover { text-decoration: underline; }

hr {
    border: 0;
    height: 1px;
    background: var(--border);
    margin: 2em 0;
}

ul, ol { padding-left: 1.8em; }
li { margin: 0.25em 0; }
li.task-list-item { list-style: none; margin-left: -1.4em; }
li.task-list-item input { margin-right: 0.5em; }

.footnotes { font-size: 0.9em; color: var(--muted); }

mark, .mdpad-active-line { background-color: var(--mark); }
";

    private const string ScriptTemplate = @"(function () {
    'use strict';

    var content = document.getElementById('content');

    function clamp(value, min, max) {
        return Math.min(max, Math.max(min, value));
    }

    function scroller() {
        return document.scrollingElement || document.documentElement;
    }

    // Broken local images are the classic symptom of a bad resource mapping;
    // marking them makes the cause visible instead of silently blank.
    function flagBrokenImages() {
        var images = content.querySelectorAll('img');
        for (var i = 0; i < images.length; i++) {
            (function (img) {
                img.addEventListener('error', function () {
                    img.classList.add('mdpad-broken');
                    if (!img.alt) { img.alt = '画像を読み込めません'; }
                });
            })(images[i]);
        }
    }

    function render(html) {
        var el = scroller();
        var previousTop = el.scrollTop;
        var previousHeight = el.scrollHeight;

        content.innerHTML = html;
        flagBrokenImages();

        // Keep the same relative position so the view does not jump to the top
        // on every keystroke while the document grows.
        var newHeight = el.scrollHeight;
        if (previousHeight > 0 && newHeight > 0 && Math.abs(newHeight - previousHeight) > 1) {
            el.scrollTop = clamp(previousTop * (newHeight / previousHeight), 0, newHeight);
        } else {
            el.scrollTop = previousTop;
        }
    }

    function blocks() {
        return content.querySelectorAll('[data-source-line]');
    }

    // Maps an editor line onto a pixel offset, interpolating between the two
    // annotated blocks that surround it.
    function offsetForLine(line) {
        var nodes = blocks();
        if (nodes.length === 0) { return null; }

        var before = null;
        var after = null;

        for (var i = 0; i < nodes.length; i++) {
            var nodeLine = parseInt(nodes[i].getAttribute('data-source-line'), 10);
            if (isNaN(nodeLine)) { continue; }

            if (nodeLine <= line) {
                before = { line: nodeLine, node: nodes[i] };
            } else {
                after = { line: nodeLine, node: nodes[i] };
                break;
            }
        }

        if (!before) { return after ? after.node.offsetTop : 0; }
        if (!after) { return before.node.offsetTop; }

        var span = after.line - before.line;
        if (span <= 0) { return before.node.offsetTop; }

        var ratio = (line - before.line) / span;
        return before.node.offsetTop + (after.node.offsetTop - before.node.offsetTop) * ratio;
    }

    function scrollToLine(line) {
        var offset = offsetForLine(line);
        if (offset === null) { return; }

        var el = scroller();
        el.scrollTop = clamp(offset - 40, 0, el.scrollHeight);
    }

    function setTheme(theme) {
        document.documentElement.setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
    }

    // In-page anchors must scroll rather than navigate, otherwise the host
    // cancels them as an external navigation.
    document.addEventListener('click', function (e) {
        var anchor = e.target && e.target.closest ? e.target.closest('a') : null;
        if (!anchor) { return; }

        var href = anchor.getAttribute('href') || '';
        if (href.charAt(0) !== '#') { return; }

        e.preventDefault();
        var target = document.getElementById(decodeURIComponent(href.slice(1)));
        if (target) { target.scrollIntoView({ block: 'start' }); }
    });

    window.__mdpad = {
        render: render,
        scrollToLine: scrollToLine,
        setTheme: setTheme
    };
})();
";
}
