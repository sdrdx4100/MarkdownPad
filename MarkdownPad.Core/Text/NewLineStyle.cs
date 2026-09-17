namespace MarkdownPad.Core.Text;

public enum NewLineStyle
{
    Crlf,
    Lf,
    Cr,
}

public static class NewLineStyleExtensions
{
    public static string ToSequence(this NewLineStyle style) => style switch
    {
        NewLineStyle.Lf => "\n",
        NewLineStyle.Cr => "\r",
        _ => "\r\n",
    };

    public static string ToDisplayName(this NewLineStyle style) => style switch
    {
        NewLineStyle.Lf => "LF",
        NewLineStyle.Cr => "CR",
        _ => "CRLF",
    };
}

public static class NewLineDetector
{
    /// <summary>
    /// Picks the dominant line ending. A file with no line break at all is
    /// reported as CRLF, matching what a Windows editor would write.
    /// </summary>
    public static NewLineStyle Detect(string text)
    {
        int crlf = 0, lf = 0, cr = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    crlf++;
                    i++;
                }
                else
                {
                    cr++;
                }
            }
            else if (c == '\n')
            {
                lf++;
            }
        }

        if (crlf == 0 && lf == 0 && cr == 0) return NewLineStyle.Crlf;
        if (lf > crlf && lf >= cr) return NewLineStyle.Lf;
        if (cr > crlf && cr > lf) return NewLineStyle.Cr;
        return NewLineStyle.Crlf;
    }

    /// <summary>Normalizes every line ending in <paramref name="text"/> to LF.</summary>
    public static string NormalizeToLf(string text)
        => text.Replace("\r\n", "\n").Replace('\r', '\n');

    /// <summary>Rewrites every line ending to <paramref name="style"/>.</summary>
    public static string ConvertTo(string text, NewLineStyle style)
    {
        string lf = NormalizeToLf(text);
        return style == NewLineStyle.Lf ? lf : lf.Replace("\n", style.ToSequence());
    }
}
