using System.Text;

namespace MarkdownPad.Core.Text;

/// <summary>A document loaded from disk, with the format needed to save it back.</summary>
public sealed record LoadedTextFile(string Text, TextDocumentFormat Format);

/// <summary>
/// Reads and writes text files while preserving the original encoding, BOM and
/// line endings. The editor always works on LF-normalized text; the original
/// line ending is reapplied on save.
/// </summary>
public static class TextFileService
{
    public static LoadedTextFile Load(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        return Load(bytes);
    }

    public static LoadedTextFile Load(byte[] bytes)
    {
        var detected = EncodingDetector.Detect(bytes);
        string raw = Decode(bytes, detected);
        var newLine = NewLineDetector.Detect(raw);

        return new LoadedTextFile(
            NewLineDetector.NormalizeToLf(raw),
            new TextDocumentFormat(detected.Encoding, detected.HasBom, newLine, detected.DisplayName));
    }

    public static void Save(string path, string text, TextDocumentFormat format)
        => File.WriteAllBytes(path, Encode(text, format));

    /// <summary>
    /// Produces the exact bytes that <see cref="Save"/> would write. Separated out
    /// so the encoding behaviour can be asserted without touching the file system.
    /// </summary>
    public static byte[] Encode(string text, TextDocumentFormat format)
    {
        string withNewLines = NewLineDetector.ConvertTo(text, format.NewLine);

        // Build a non-throwing clone: characters the target code page cannot
        // represent become '?' rather than aborting the save.
        var encoding = (Encoding)format.Encoding.Clone();
        encoding.EncoderFallback = EncoderFallback.ReplacementFallback;

        byte[] body = encoding.GetBytes(withNewLines);
        byte[] preamble = format.HasBom ? format.Encoding.GetPreamble() : Array.Empty<byte>();

        if (preamble.Length == 0) return body;

        byte[] result = new byte[preamble.Length + body.Length];
        preamble.CopyTo(result, 0);
        body.CopyTo(result, preamble.Length);
        return result;
    }

    /// <summary>
    /// Reports the first character that would be lost if <paramref name="text"/>
    /// were saved with <paramref name="format"/>, so the user can be warned
    /// before the data is silently replaced with '?'. Iterates by rune so that
    /// an emoji is reported as itself rather than as half a surrogate pair.
    /// </summary>
    public static bool TryFindUnencodableText(string text, TextDocumentFormat format, out string character)
    {
        character = string.Empty;

        var strict = (Encoding)format.Encoding.Clone();
        strict.EncoderFallback = EncoderFallback.ExceptionFallback;

        // One encode of the whole document answers the common case. Only when
        // that fails is it worth walking the text rune by rune to name the
        // offender, which costs an encoder call per character.
        try
        {
            _ = strict.GetBytes(text);
            return false;
        }
        catch (EncoderFallbackException)
        {
            // Fall through to locate the specific character.
        }

        foreach (var rune in text.EnumerateRunes())
        {
            string value = rune.ToString();
            try
            {
                _ = strict.GetBytes(value);
            }
            catch (EncoderFallbackException)
            {
                character = value;
                return true;
            }
        }

        return false;
    }

    private static string Decode(byte[] bytes, DetectedEncoding detected)
    {
        var lenient = (Encoding)detected.Encoding.Clone();
        lenient.DecoderFallback = DecoderFallback.ReplacementFallback;

        int offset = 0;
        if (detected.HasBom)
        {
            byte[] preamble = detected.Encoding.GetPreamble();
            if (preamble.Length > 0 && bytes.Length >= preamble.Length)
            {
                offset = preamble.Length;
            }
        }

        return lenient.GetString(bytes, offset, bytes.Length - offset);
    }
}
