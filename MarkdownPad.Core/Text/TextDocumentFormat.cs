using System.Text;

namespace MarkdownPad.Core.Text;

/// <summary>
/// Everything needed to write a document back exactly the way it was read:
/// the encoding, whether it carried a BOM, and its line ending style.
/// </summary>
public sealed record TextDocumentFormat(Encoding Encoding, bool HasBom, NewLineStyle NewLine, string EncodingDisplayName)
{
    public static TextDocumentFormat Default { get; } =
        new(TextEncodings.Utf8NoBom, false, NewLineStyle.Crlf, "UTF-8");

    public string StatusText => $"{EncodingDisplayName} / {NewLine.ToDisplayName()}";
}
