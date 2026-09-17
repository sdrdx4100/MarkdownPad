using System.Text;

namespace MarkdownPad.Core.Text;

/// <summary>The encoding and BOM state deduced for a byte buffer.</summary>
public sealed record DetectedEncoding(Encoding Encoding, bool HasBom, string DisplayName);

/// <summary>
/// Detects the encoding of a text file. The strategy is deliberately simple and
/// predictable: an explicit BOM wins, then a strict UTF-8 decode, then the
/// legacy Japanese code page, and finally a lossy Latin-1 fallback so that a
/// file always opens rather than throwing.
/// </summary>
public static class EncodingDetector
{
    private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };
    private static readonly byte[] Utf16LeBom = { 0xFF, 0xFE };
    private static readonly byte[] Utf16BeBom = { 0xFE, 0xFF };
    private static readonly byte[] Utf32LeBom = { 0xFF, 0xFE, 0x00, 0x00 };
    private static readonly byte[] Utf32BeBom = { 0x00, 0x00, 0xFE, 0xFF };

    public static DetectedEncoding Detect(ReadOnlySpan<byte> bytes)
    {
        TextEncodings.EnsureProviderRegistered();

        // UTF-32 LE shares its first two bytes with UTF-16 LE, so test it first.
        if (StartsWith(bytes, Utf32LeBom)) return new DetectedEncoding(new UTF32Encoding(false, true), true, "UTF-32 LE");
        if (StartsWith(bytes, Utf32BeBom)) return new DetectedEncoding(new UTF32Encoding(true, true), true, "UTF-32 BE");
        if (StartsWith(bytes, Utf8Bom)) return new DetectedEncoding(TextEncodings.Utf8Bom, true, "UTF-8 (BOM付き)");
        if (StartsWith(bytes, Utf16LeBom)) return new DetectedEncoding(new UnicodeEncoding(false, true), true, "UTF-16 LE");
        if (StartsWith(bytes, Utf16BeBom)) return new DetectedEncoding(new UnicodeEncoding(true, true), true, "UTF-16 BE");

        if (bytes.IsEmpty || IsAscii(bytes) || IsValidUtf8(bytes))
        {
            return new DetectedEncoding(TextEncodings.Utf8NoBom, false, "UTF-8");
        }

        var shiftJis = TextEncodings.ShiftJis;
        if (shiftJis is not null && DecodesCleanly(shiftJis, bytes))
        {
            return new DetectedEncoding(shiftJis, false, "Shift_JIS");
        }

        // Latin-1 maps every byte to a character, so this never fails and keeps
        // the original bytes recoverable on a round-trip.
        return new DetectedEncoding(Encoding.Latin1, false, "Latin-1");
    }

    private static bool StartsWith(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> prefix)
        => bytes.Length >= prefix.Length && bytes[..prefix.Length].SequenceEqual(prefix);

    private static bool IsAscii(ReadOnlySpan<byte> bytes)
    {
        foreach (byte b in bytes)
        {
            if (b > 0x7F) return false;
        }
        return true;
    }

    /// <summary>
    /// Validates UTF-8 by hand rather than by catching a decoder exception:
    /// overlong forms, surrogate halves and out-of-range code points are all
    /// rejected, which is what keeps Shift_JIS text from being mistaken for UTF-8.
    /// </summary>
    internal static bool IsValidUtf8(ReadOnlySpan<byte> bytes)
    {
        int i = 0;
        while (i < bytes.Length)
        {
            byte b = bytes[i];

            if (b <= 0x7F)
            {
                i++;
                continue;
            }

            int extra;
            int codePoint;

            if (b >= 0xC2 && b <= 0xDF)
            {
                extra = 1;
                codePoint = b & 0x1F;
            }
            else if (b >= 0xE0 && b <= 0xEF)
            {
                extra = 2;
                codePoint = b & 0x0F;
            }
            else if (b >= 0xF0 && b <= 0xF4)
            {
                extra = 3;
                codePoint = b & 0x07;
            }
            else
            {
                // 0x80-0xC1 and 0xF5-0xFF never start a valid sequence.
                return false;
            }

            if (i + extra >= bytes.Length) return false;

            for (int k = 1; k <= extra; k++)
            {
                byte cont = bytes[i + k];
                if ((cont & 0xC0) != 0x80) return false;
                codePoint = (codePoint << 6) | (cont & 0x3F);
            }

            if (extra == 2 && codePoint < 0x800) return false;      // overlong
            if (extra == 3 && codePoint < 0x10000) return false;    // overlong
            if (codePoint > 0x10FFFF) return false;                 // out of range
            if (codePoint is >= 0xD800 and <= 0xDFFF) return false; // lone surrogate

            i += extra + 1;
        }

        return true;
    }

    private static bool DecodesCleanly(Encoding encoding, ReadOnlySpan<byte> bytes)
    {
        try
        {
            var strict = (Encoding)encoding.Clone();
            strict.DecoderFallback = DecoderFallback.ExceptionFallback;
            _ = strict.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
