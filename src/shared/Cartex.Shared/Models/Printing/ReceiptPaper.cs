namespace Cartex.Shared.Models.Printing;

/// Termal chek kengligi (bir qatordagi belgilar soni). 58mm qog'oz ≈ 32, 80mm ≈ 42–48;
/// istalgan real qiymat qabul qilinadi, ro'yxatdagilar faqat tez tanlov uchun.
public static class ReceiptPaper
{
    public const int MinWidth = 24;
    public const int MaxWidth = 64;
    public const int DefaultWidth = 32;

    public static readonly int[] CommonWidths = [32, 40, 42, 48, 64];

    public static bool IsValid(int width) => width is >= MinWidth and <= MaxWidth;

    /// Termal boshning nuqta zichligi o'zgarmaydi, shuning uchun yozuvni kattalashtirishning
    /// yagona yo'li — bir qatordagi belgilar sonini kamaytirish.
    public static int ApplyTextSize(int columns, string? size) => size switch
    {
        "xlarge" => (int)Math.Round(columns * 0.72),
        "large" => (int)Math.Round(columns * 0.84),
        _ => columns
    };

    /// Matn rejimida printerning o'z shrifti faqat butun songa kattalashadi (`GS !`),
    /// shuning uchun bu yerda 1x yoki 2x bo'ladi — va aynan shu tiniqlikni saqlaydi.
    public static int TextMagnification(string? size) => size == "double" ? 2 : 1;

    /// Rasm har doim qog'ozning to'liq kengligida chiziladi. Yozuv o'lchami faqat
    /// ustunlar sonini kamaytiradi; agar u rasm kengligiga ham ta'sir qilsa, chek
    /// torayib qog'ozning chap tomoniga surilib chiqadi. Shuning uchun bu yerga
    /// **nominal** ustunlar soni beriladi, o'lcham qo'llanganidan keyingisi emas.
    public static int RasterDots(int explicitDots, int? driverDots, int nominalColumns)
    {
        if (explicitDots is >= 128 and <= 2048) return Align8(explicitDots);
        if (driverDots is >= 128 and <= 2048) return Align8(driverDots.Value);
        return Align8(Sanitize(nominalColumns) * 12);
    }

    private static int Align8(int value) => (value + 7) / 8 * 8;

    public static int Sanitize(int width, int fallback = DefaultWidth) =>
        IsValid(width) ? width : fallback;

    /// Drayver bergan qog'oz kengligidan (mm) printer sinfi orqali ustunlar soni:
    /// 58 mm sinfi → 32, 80 mm sinfi → 48, 112 mm sinfi → 64. 120 mm dan keng qog'oz
    /// chek printeri emas, 45 mm dan tori noma'lum.
    public static int? ColumnsForMm(double? paperWidthMm) => paperWidthMm switch
    {
        null or > 120 => null,
        >= 100 => 64,
        >= 68 => 48,
        >= 45 => 32,
        _ => null
    };
}
