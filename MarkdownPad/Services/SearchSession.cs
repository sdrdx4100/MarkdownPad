using MarkdownPad.Core.Text;

namespace MarkdownPad.Services;

/// <summary>
/// The search the user last ran, shared between the find dialog and the
/// F3 / Shift+F3 shortcuts so that repeating a search does not require the
/// dialog to be open.
/// </summary>
public sealed class SearchSession
{
    public SearchQuery? Query { get; set; }

    public bool Wrap { get; set; } = true;

    public bool HasQuery => Query is { IsUsable: true };
}
