namespace MarkdownPad.Core.Text;

/// <summary>
/// The result of a formatting command: the span of the original document to
/// replace, the text to put there, and where the selection should end up.
/// Expressed as a single replacement so the editor can apply it as one
/// undoable change.
/// </summary>
public sealed record EditOperation(int Start, int Length, string ReplacementText, int SelectionStart, int SelectionLength)
{
    public static EditOperation None(int caret) => new(caret, 0, string.Empty, caret, 0);
}
