namespace Cartex.Shared.Localization;

public static class ReceiptTexts
{
    public const string DefaultLanguage = "uz-latn";
    public static readonly string[] Languages = ["uz-latn", "uz-cyrl", "ru", "en"];

    private static readonly Dictionary<string, Dictionary<string, string>> Texts = new()
    {
        ["receipt_no"] = new() { ["uz-latn"] = "CHEK №", ["uz-cyrl"] = "ЧЕК №", ["ru"] = "ЧЕК №", ["en"] = "RECEIPT #" },
        ["cashier"] = new() { ["uz-latn"] = "Kassir", ["uz-cyrl"] = "Кассир", ["ru"] = "Кассир", ["en"] = "Cashier" },
        ["customer"] = new() { ["uz-latn"] = "Mijoz", ["uz-cyrl"] = "Мижоз", ["ru"] = "Клиент", ["en"] = "Customer" },
        ["item"] = new() { ["uz-latn"] = "Mahsulot", ["uz-cyrl"] = "Маҳсулот", ["ru"] = "Товар", ["en"] = "Item" },
        ["qty"] = new() { ["uz-latn"] = "Miqdor", ["uz-cyrl"] = "Миқдор", ["ru"] = "Кол-во", ["en"] = "Qty" },
        ["price"] = new() { ["uz-latn"] = "Narx", ["uz-cyrl"] = "Нарх", ["ru"] = "Цена", ["en"] = "Price" },
        ["amount"] = new() { ["uz-latn"] = "Summa", ["uz-cyrl"] = "Сумма", ["ru"] = "Сумма", ["en"] = "Amount" },
        ["subtotal"] = new() { ["uz-latn"] = "Oraliq jami", ["uz-cyrl"] = "Оралиқ жами", ["ru"] = "Подытог", ["en"] = "Subtotal" },
        ["discount"] = new() { ["uz-latn"] = "Chegirma", ["uz-cyrl"] = "Чегирма", ["ru"] = "Скидка", ["en"] = "Discount" },
        ["total"] = new() { ["uz-latn"] = "JAMI", ["uz-cyrl"] = "ЖАМИ", ["ru"] = "ИТОГО", ["en"] = "TOTAL" },
        ["cash"] = new() { ["uz-latn"] = "Naqd", ["uz-cyrl"] = "Нақд", ["ru"] = "Наличные", ["en"] = "Cash" },
        ["card"] = new() { ["uz-latn"] = "Karta", ["uz-cyrl"] = "Карта", ["ru"] = "Карта", ["en"] = "Card" },
        ["bonus"] = new() { ["uz-latn"] = "Bonus", ["uz-cyrl"] = "Бонус", ["ru"] = "Бонус", ["en"] = "Bonus" },
        ["change"] = new() { ["uz-latn"] = "Qaytim", ["uz-cyrl"] = "Қайтим", ["ru"] = "Сдача", ["en"] = "Change" },
        ["debt"] = new() { ["uz-latn"] = "Qarz", ["uz-cyrl"] = "Қарз", ["ru"] = "Долг", ["en"] = "Debt" },
        ["cashback"] = new() { ["uz-latn"] = "Cashback to'plandi", ["uz-cyrl"] = "Cashback тўпланди", ["ru"] = "Начислен кэшбэк", ["en"] = "Cashback earned" },
        ["paid"] = new() { ["uz-latn"] = "TO'LANDI", ["uz-cyrl"] = "ТЎЛАНДИ", ["ru"] = "ОПЛАЧЕНО", ["en"] = "PAID" },
        ["unpaid"] = new() { ["uz-latn"] = "QARZ", ["uz-cyrl"] = "ҚАРЗ", ["ru"] = "ДОЛГ", ["en"] = "DEBT" },
        ["thanks"] = new() { ["uz-latn"] = "Xaridingiz uchun rahmat!", ["uz-cyrl"] = "Харидингиз учун раҳмат!", ["ru"] = "Спасибо за покупку!", ["en"] = "Thank you for your purchase!" },
        ["your_receipt"] = new() { ["uz-latn"] = "Chek", ["uz-cyrl"] = "Чек", ["ru"] = "Чек", ["en"] = "Receipt" },
        ["download_pdf"] = new() { ["uz-latn"] = "PDF yuklab olish", ["uz-cyrl"] = "PDF юклаб олиш", ["ru"] = "Скачать PDF", ["en"] = "Download PDF" },
        ["your_purchase"] = new() { ["uz-latn"] = "Xaridingiz", ["uz-cyrl"] = "Харидингиз", ["ru"] = "Ваша покупка", ["en"] = "Your purchase" }
    };

    public static string Get(string key, string? lang)
    {
        var map = Texts[key];
        return lang is not null && map.TryGetValue(lang, out var value) ? value : map[DefaultLanguage];
    }

    public static string PaymentLabel(string method, string? lang) => method switch
    {
        "Cash" => Get("cash", lang),
        "Card" => Get("card", lang),
        "Bonus" => Get("bonus", lang),
        _ => method
    };
}
