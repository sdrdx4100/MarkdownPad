using System.Text;

namespace MarkdownPad.Core.Text;

/// <summary>
/// Central place for the encodings the application offers, plus the one-time
/// registration of <see cref="CodePagesEncodingProvider"/> that .NET requires
/// before legacy code pages such as Shift_JIS (CP932) can be resolved.
/// </summary>
public static class TextEncodings
{
    private static bool _registered;
    private static readonly object Gate = new();

    public const int ShiftJisCodePage = 932;

    /// <summary>UTF-8 that never emits a BOM and throws on malformed input.</summary>
    public static readonly Encoding Utf8NoBom = new UTF8Encoding(false, true);

    /// <summary>UTF-8 that emits a BOM.</summary>
    public static readonly Encoding Utf8Bom = new UTF8Encoding(true, true);

    /// <summary>
    /// Registers the code pages provider. Safe to call repeatedly and from
    /// multiple threads; only the first call does any work.
    /// </summary>
    public static void EnsureProviderRegistered()
    {
        if (_registered) return;

        lock (Gate)
        {
            if (_registered) return;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _registered = true;
        }
    }

    /// <summary>
    /// Resolves Shift_JIS (CP932), or <c>null</c> when the platform cannot provide it.
    /// </summary>
    public static Encoding? ShiftJis
    {
        get
        {
            EnsureProviderRegistered();
            try
            {
                return Encoding.GetEncoding(ShiftJisCodePage);
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }
    }

    /// <summary>The encodings offered in the UI, in display order.</summary>
    public static IReadOnlyList<EncodingOption> Options { get; } = BuildOptions();

    private static EncodingOption[] BuildOptions()
    {
        var options = new List<EncodingOption>
        {
            new("UTF-8", Utf8NoBom, false),
            new("UTF-8 (BOM付き)", Utf8Bom, true),
            new("UTF-16 LE", new UnicodeEncoding(false, true), true),
            new("UTF-16 BE", new UnicodeEncoding(true, true), true),
        };

        var sjis = ShiftJis;
        if (sjis is not null)
        {
            options.Add(new EncodingOption("Shift_JIS", sjis, false));
        }

        return options.ToArray();
    }
}

/// <summary>A user-selectable encoding together with its BOM preference.</summary>
public sealed record EncodingOption(string DisplayName, Encoding Encoding, bool UseBom);
