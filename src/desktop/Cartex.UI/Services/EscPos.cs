using System.Text;
using Cartex.Shared.Models.Printing;

namespace Cartex.UI.Services;

public sealed record EscPosCharset(string Key, string Label, int CodePage, int CodeTable);

public sealed record EscPosProfile(
    EscPosCharset Charset,
    int CodeTable,
    string CutMode,
    int FeedLines,
    bool FoldCyrillic = false,
    int Magnify = 1)
{
    public static EscPosProfile From(PrinterSettings s) => new(
        EscPos.ResolveCharset(s.ReceiptCharset),
        s.ReceiptCodeTable,
        s.ReceiptCutMode is "full" or "none" ? s.ReceiptCutMode : "partial",
        Math.Clamp(s.ReceiptFeedBeforeCut, 0, 12),
        s.ReceiptFoldCyrillic,
        ReceiptPaper.TextMagnification(s.ReceiptTextSize));
}

/// Termal printer bilan uning o'z tilida gaplashadi: har ish ESC @ bilan toza holatdan
/// boshlanadi, xitoycha ikki-baytli rejim o'chiriladi, matn tanlangan kod jadvalida
/// yuboriladi va oxirida qog'oz surilib kesiladi. UTF-8 to'g'ridan-to'g'ri yuborilmaydi —
/// aksariyat printerlar uni bilmaydi va baytlarni tasodifiy belgilarga aylantiradi.
public static class EscPos
{
    static EscPos() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static readonly EscPosCharset[] Charsets =
    [
        new("cp437", "Lotin (CP437)", 437, 0),
        new("cp850", "Lotin (CP850)", 850, 2),
        new("cp852", "Markaziy Yevropa (CP852)", 852, 18),
        new("cp858", "Lotin + € (CP858)", 858, 19),
        new("cp866", "Kirill (CP866)", 866, 17),
        new("cp1251", "Kirill (Windows-1251)", 1251, 46),
        new("cp1252", "G'arbiy Yevropa (Windows-1252)", 1252, 16),
        new("utf8", "UTF-8 (printer qo'llasa)", 65001, -1)
    ];

    public static EscPosCharset ResolveCharset(string? key) =>
        Charsets.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase)) ?? Charsets[0];

    private static readonly Dictionary<int, Encoding> Encodings = [];

    private static Encoding EncodingFor(int codePage)
    {
        lock (Encodings)
        {
            if (!Encodings.TryGetValue(codePage, out var encoding))
            {
                encoding = codePage == 65001
                    ? new UTF8Encoding(false)
                    : Encoding.GetEncoding(codePage, new EncoderReplacementFallback("?"), DecoderFallback.ReplacementFallback);
                Encodings[codePage] = encoding;
            }
            return encoding;
        }
    }

    /// Hech bir PC kod jadvalida o'zbek kirillchasining қ, ғ, ҳ harflari yo'q, ў esa
    /// faqat CP1251 da bor. Printer ularni chop eta olmasa matn rejimida "?" chiqadi;
    /// almashtirish o'qilishi mumkin bo'lgan eng yaqin harfni beradi.
    private static char Fold(char ch) => ch switch
    {
        'қ' => 'к', 'Қ' => 'К',
        'ғ' => 'г', 'Ғ' => 'Г',
        'ҳ' => 'х', 'Ҳ' => 'Х',
        'ў' => 'о', 'Ў' => 'О',
        _ => ch
    };

    public static string Sanitize(string text, bool foldCyrillic = false)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var raw in text)
        {
            var ch = foldCyrillic ? Fold(raw) : raw;
            switch (ch)
            {
                case '’' or '‘' or 'ʻ' or 'ʼ' or '´' or '‚': sb.Append('\''); break;
                case '“' or '”' or '„' or '«' or '»': sb.Append('"'); break;
                case '—' or '–' or '―' or '−': sb.Append('-'); break;
                case '№': sb.Append("No"); break;
                case '…': sb.Append("..."); break;
                case ' ' or ' ': sb.Append(' '); break;
                case '≈': sb.Append('~'); break;
                case '×': sb.Append('x'); break;
                default: sb.Append(ch); break;
            }
        }
        return sb.ToString();
    }

    public static string ResolveOutputMode(
        string? requestedMode,
        string text,
        EscPosCharset charset,
        bool foldCyrillic = false)
    {
        if (string.Equals(requestedMode, "graphic", StringComparison.OrdinalIgnoreCase)) return "graphic";
        if (string.Equals(requestedMode, "text", StringComparison.OrdinalIgnoreCase)) return "text";
        if (RoundTrip(Sanitize(text), charset) == Sanitize(text)) return "text";
        // Almashtirish yoqilgan bo'lsa faqat shu to'rt harf sabab rasterga o'tilmaydi:
        // printerning o'z shrifti tiniqroq va tezroq.
        return foldCyrillic && RoundTrip(Sanitize(text, true), charset) == Sanitize(text, true)
            ? "text"
            : "graphic";
    }

    public static string PreviewText(
        string text,
        string? requestedMode,
        EscPosCharset charset,
        bool foldCyrillic = false) =>
        ResolveOutputMode(requestedMode, text, charset, foldCyrillic) == "graphic"
            ? text
            : RoundTrip(Sanitize(text, foldCyrillic), charset);

    public static string RoundTrip(string text, EscPosCharset charset) =>
        EncodingFor(charset.CodePage).GetString(EncodingFor(charset.CodePage).GetBytes(text));

    public static byte[] BuildDocument(
        string text,
        string? qrContent,
        byte[]? logoRasterBytes,
        EscPosProfile profile,
        byte[]? textRasterBytes = null)
    {
        if (textRasterBytes is not null)
            return BuildDocument(
                ReceiptTextDocument.Plain(text, 32),
                qrContent,
                logoRasterBytes,
                profile,
                new ReceiptRasterDocument(textRasterBytes, []));

        var body = EncodingFor(profile.Charset.CodePage).GetBytes(Sanitize(text, profile.FoldCyrillic));
        var output = new List<byte>(body.Length + (logoRasterBytes?.Length ?? 0) + 96);
        output.AddRange([0x1B, 0x40]);
        if (profile.Charset.CodePage != 65001)
        {
            output.AddRange([0x1C, 0x2E]);
            var table = profile.CodeTable is >= 0 and <= 255 ? profile.CodeTable : profile.Charset.CodeTable;
            output.AddRange([0x1B, 0x74, (byte)table]);
        }
        if (logoRasterBytes is { Length: > 0 })
            output.AddRange(logoRasterBytes);
        output.AddRange(body);
        AddQr(output, qrContent);
        Finish(output, profile);
        return [.. output];
    }

    public static byte[] BuildDocument(
        ReceiptTextDocument document,
        string? qrContent,
        byte[]? logoRasterBytes,
        EscPosProfile profile,
        ReceiptRasterDocument? raster)
    {
        var output = new List<byte>(document.Text.Length * 2 + (logoRasterBytes?.Length ?? 0)
            + (raster?.BeforeQr.Length ?? 0) + (raster?.AfterQr.Length ?? 0) + 96);

        output.AddRange([0x1B, 0x40]);
        if (raster is null && profile.Charset.CodePage != 65001)
        {
            output.AddRange([0x1C, 0x2E]);
            var table = profile.CodeTable is >= 0 and <= 255 ? profile.CodeTable : profile.Charset.CodeTable;
            output.AddRange([0x1B, 0x74, (byte)table]);
        }

        if (logoRasterBytes is { Length: > 0 })
            output.AddRange(logoRasterBytes);

        if (raster is null)
            AddText(output, document.BeforeQr, document.Width, profile.Charset, profile.FoldCyrillic, profile.Magnify);
        else
            output.AddRange(raster.BeforeQr);

        AddQr(output, qrContent);

        if (raster is null)
            AddText(output, document.AfterQr, document.Width, profile.Charset, profile.FoldCyrillic, profile.Magnify);
        else if (raster.AfterQr.Length > 0)
            output.AddRange(raster.AfterQr);

        Finish(output, profile);
        return [.. output];
    }

    private static void AddQr(List<byte> output, string? qrContent)
    {
        if (string.IsNullOrWhiteSpace(qrContent)) return;
        output.AddRange([0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00]);
        output.AddRange([0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, 0x05]);
        output.AddRange([0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, 0x31]);
        var data = Encoding.UTF8.GetBytes(qrContent);
        var length = data.Length + 3;
        output.AddRange([0x1D, 0x28, 0x6B, (byte)(length & 0xFF), (byte)(length >> 8), 0x31, 0x50, 0x30]);
        output.AddRange(data);
        output.AddRange([0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30, 0x0A, 0x0A]);
    }

    private static void Finish(List<byte> output, EscPosProfile profile)
    {
        if (profile.FeedLines > 0)
            output.AddRange([0x1B, 0x64, (byte)profile.FeedLines]);
        if (profile.CutMode != "none")
            output.AddRange([0x1D, 0x56, profile.CutMode == "full" ? (byte)0x41 : (byte)0x42, 0x00]);
    }

    /// Yozuvni kattalashtirishni printerning o'ziga topshiramiz: `GS !` uning bitmap
    /// shriftini piksel-ikkilash bilan kattalashtiradi, shuning uchun natija katta
    /// bo'lsa ham 1x kabi tiniq qoladi. Rasterda kattalashtirilgan harfning shtrixi
    /// qalinlashib termal qog'ozda yoyilib ketadi.
    private static void AddText(
        List<byte> output,
        IReadOnlyList<ReceiptTextLine> lines,
        int width,
        EscPosCharset charset,
        bool foldCyrillic,
        int magnify)
    {
        var encoding = EncodingFor(charset.CodePage);
        var scale = Math.Clamp(magnify, 1, 4) - 1;
        if (scale > 0)
            output.AddRange([0x1D, 0x21, (byte)((scale << 4) | scale)]);
        foreach (var line in lines)
        {
            if (line.Style == ReceiptTextStyle.Total)
                output.AddRange([0x1D, 0x42, 0x01]);
            var text = line.Centered ? Center(line.Text, width) : line.Text;
            output.AddRange(encoding.GetBytes(Sanitize(text + "\n", foldCyrillic)));
            if (line.Style == ReceiptTextStyle.Total)
                output.AddRange([0x1D, 0x42, 0x00]);
        }
        if (scale > 0)
            output.AddRange([0x1D, 0x21, 0x00]);
    }

    private static string Center(string value, int width)
    {
        if (value.Length >= width) return value;
        return new string(' ', (width - value.Length) / 2) + value;
    }
}
