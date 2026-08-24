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
        ["unit"] = new() { ["uz-latn"] = "O'lchov", ["uz-cyrl"] = "Ўлчов", ["ru"] = "Ед.", ["en"] = "Unit" },
        ["unit_piece"] = new() { ["uz-latn"] = "dona", ["uz-cyrl"] = "дона", ["ru"] = "шт.", ["en"] = "pcs" },
        ["sample_item_one"] = new() { ["uz-latn"] = "Elektr kabeli", ["uz-cyrl"] = "Электр кабели", ["ru"] = "Электрический кабель", ["en"] = "Electrical cable" },
        ["sample_item_two"] = new() { ["uz-latn"] = "LED chiroq", ["uz-cyrl"] = "LED чироқ", ["ru"] = "Светодиодная лампа", ["en"] = "LED lamp" },
        ["sample_item_three"] = new() { ["uz-latn"] = "Mis quvur", ["uz-cyrl"] = "Мис қувур", ["ru"] = "Медная труба", ["en"] = "Copper pipe" },
        ["sample_item_four"] = new() { ["uz-latn"] = "Rozetka", ["uz-cyrl"] = "Розетка", ["ru"] = "Розетка", ["en"] = "Power socket" },
        ["sample_item_five"] = new() { ["uz-latn"] = "Mahkamlash to'plami", ["uz-cyrl"] = "Маҳкамлаш тўплами", ["ru"] = "Комплект крепежа", ["en"] = "Fastener set" },
        ["qty"] = new() { ["uz-latn"] = "Miqdor", ["uz-cyrl"] = "Миқдор", ["ru"] = "Кол-во", ["en"] = "Qty" },
        ["price"] = new() { ["uz-latn"] = "Narx", ["uz-cyrl"] = "Нарх", ["ru"] = "Цена", ["en"] = "Price" },
        ["amount"] = new() { ["uz-latn"] = "Summa", ["uz-cyrl"] = "Сумма", ["ru"] = "Сумма", ["en"] = "Amount" },
        ["subtotal"] = new() { ["uz-latn"] = "Oraliq jami", ["uz-cyrl"] = "Оралиқ жами", ["ru"] = "Подытог", ["en"] = "Subtotal" },
        ["discount"] = new() { ["uz-latn"] = "Chegirma", ["uz-cyrl"] = "Чегирма", ["ru"] = "Скидка", ["en"] = "Discount" },
        ["total"] = new() { ["uz-latn"] = "JAMI", ["uz-cyrl"] = "ЖАМИ", ["ru"] = "ИТОГО", ["en"] = "TOTAL" },
        ["cash"] = new() { ["uz-latn"] = "Naqd", ["uz-cyrl"] = "Нақд", ["ru"] = "Наличные", ["en"] = "Cash" },
        ["card"] = new() { ["uz-latn"] = "Karta", ["uz-cyrl"] = "Карта", ["ru"] = "Карта", ["en"] = "Card" },
        ["bonus"] = new() { ["uz-latn"] = "Bonus", ["uz-cyrl"] = "Бонус", ["ru"] = "Бонус", ["en"] = "Bonus" },
        ["advance"] = new() { ["uz-latn"] = "Avansdan", ["uz-cyrl"] = "Авансдан", ["ru"] = "Из аванса", ["en"] = "From advance" },
        ["transfer"] = new() { ["uz-latn"] = "Mobil o'tkazma", ["uz-cyrl"] = "Мобил ўтказма", ["ru"] = "Перевод", ["en"] = "Transfer" },
        ["bank"] = new() { ["uz-latn"] = "Bank o'tkazmasi", ["uz-cyrl"] = "Банк ўтказмаси", ["ru"] = "Банковский перевод", ["en"] = "Bank transfer" },
        ["change"] = new() { ["uz-latn"] = "Qaytim", ["uz-cyrl"] = "Қайтим", ["ru"] = "Сдача", ["en"] = "Change" },
        ["credit"] = new() { ["uz-latn"] = "Hisobga yozildi", ["uz-cyrl"] = "Ҳисобга ёзилди", ["ru"] = "Зачислено на счёт", ["en"] = "Credited to account" },
        ["debt"] = new() { ["uz-latn"] = "Qarz", ["uz-cyrl"] = "Қарз", ["ru"] = "Долг", ["en"] = "Debt" },
        ["cashback"] = new() { ["uz-latn"] = "Cashback to'plandi", ["uz-cyrl"] = "Cashback тўпланди", ["ru"] = "Начислен кэшбэк", ["en"] = "Cashback earned" },
        ["paid"] = new() { ["uz-latn"] = "TO'LANDI", ["uz-cyrl"] = "ТЎЛАНДИ", ["ru"] = "ОПЛАЧЕНО", ["en"] = "PAID" },
        ["unpaid"] = new() { ["uz-latn"] = "QARZ", ["uz-cyrl"] = "ҚАРЗ", ["ru"] = "ДОЛГ", ["en"] = "DEBT" },
        ["thanks"] = new() { ["uz-latn"] = "Xaridingiz uchun rahmat!", ["uz-cyrl"] = "Харидингиз учун раҳмат!", ["ru"] = "Спасибо за покупку!", ["en"] = "Thank you for your purchase!" },
        ["qr_caption"] = new() { ["uz-latn"] = "Chekni telefonda ochish", ["uz-cyrl"] = "Чекни телефонда очиш", ["ru"] = "Открыть чек на телефоне", ["en"] = "Open receipt on phone" },
        ["your_receipt"] = new() { ["uz-latn"] = "Chek", ["uz-cyrl"] = "Чек", ["ru"] = "Чек", ["en"] = "Receipt" },
        ["download_pdf"] = new() { ["uz-latn"] = "PDF yuklab olish", ["uz-cyrl"] = "PDF юклаб олиш", ["ru"] = "Скачать PDF", ["en"] = "Download PDF" },
        ["your_purchase"] = new() { ["uz-latn"] = "Xaridingiz", ["uz-cyrl"] = "Харидингиз", ["ru"] = "Ваша покупка", ["en"] = "Your purchase" },
        ["not_receipt"] = new() { ["uz-latn"] = "OLDINDAN KO'RISH — CHEK EMAS", ["uz-cyrl"] = "ОЛДИНДАН КЎРИШ — ЧЕК ЭМАС", ["ru"] = "ПРЕДПРОСМОТР — НЕ ЧЕК", ["en"] = "PREVIEW — NOT A RECEIPT" },
        ["cart"] = new() { ["uz-latn"] = "Savat", ["uz-cyrl"] = "Сават", ["ru"] = "Корзина", ["en"] = "Cart" },
        ["seller"] = new() { ["uz-latn"] = "Sotuvchi", ["uz-cyrl"] = "Сотувчи", ["ru"] = "Продавец", ["en"] = "Seller" },
        ["note"] = new() { ["uz-latn"] = "Izoh", ["uz-cyrl"] = "Изоҳ", ["ru"] = "Примечание", ["en"] = "Note" },
        ["staff"] = new() { ["uz-latn"] = "Xodim", ["uz-cyrl"] = "Ходим", ["ru"] = "Сотрудник", ["en"] = "Staff" },
        ["warehouse"] = new() { ["uz-latn"] = "Ombor", ["uz-cyrl"] = "Омбор", ["ru"] = "Склад", ["en"] = "Warehouse" },
        ["document_no"] = new() { ["uz-latn"] = "Hujjat №", ["uz-cyrl"] = "Ҳужжат №", ["ru"] = "Документ №", ["en"] = "Document #" },
        ["return_title"] = new() { ["uz-latn"] = "MAHSULOT QAYTARISH", ["uz-cyrl"] = "МАҲСУЛОТ ҚАЙТАРИШ", ["ru"] = "ВОЗВРАТ ТОВАРА", ["en"] = "PRODUCT RETURN" },
        ["payout_title"] = new() { ["uz-latn"] = "PUL CHIQIMI", ["uz-cyrl"] = "ПУЛ ЧИҚИМИ", ["ru"] = "ВЫДАЧА ДЕНЕГ", ["en"] = "CASH PAYOUT" },
        ["payment_title"] = new() { ["uz-latn"] = "TO'LOV QABUL QILINDI", ["uz-cyrl"] = "ТЎЛОВ ҚАБУЛ ҚИЛИНДИ", ["ru"] = "ОПЛАТА ПРИНЯТА", ["en"] = "PAYMENT RECEIVED" },
        ["given"] = new() { ["uz-latn"] = "BERILDI", ["uz-cyrl"] = "БЕРИЛДИ", ["ru"] = "ВЫДАНО", ["en"] = "PAID OUT" },
        ["received"] = new() { ["uz-latn"] = "QABUL QILINDI", ["uz-cyrl"] = "ҚАБУЛ ҚИЛИНДИ", ["ru"] = "ПРИНЯТО", ["en"] = "RECEIVED" },
        ["to_advance"] = new() { ["uz-latn"] = "Avansga", ["uz-cyrl"] = "Авансга", ["ru"] = "В аванс", ["en"] = "To advance" },
        ["loan_given"] = new() { ["uz-latn"] = "Qarzga berildi", ["uz-cyrl"] = "Қарзга берилди", ["ru"] = "Выдано в долг", ["en"] = "Loaned out" },
        ["forgiven"] = new() { ["uz-latn"] = "Kechirildi", ["uz-cyrl"] = "Кечирилди", ["ru"] = "Прощено", ["en"] = "Forgiven" },
        ["debt_balance"] = new() { ["uz-latn"] = "QARZ QOLDIG'I", ["uz-cyrl"] = "ҚАРЗ ҚОЛДИҒИ", ["ru"] = "ОСТАТОК ДОЛГА", ["en"] = "DEBT BALANCE" },
        ["advance_balance"] = new() { ["uz-latn"] = "AVANS QOLDIG'I", ["uz-cyrl"] = "АВАНС ҚОЛДИҒИ", ["ru"] = "ОСТАТОК АВАНСА", ["en"] = "ADVANCE BALANCE" },
        ["from_debt"] = new() { ["uz-latn"] = "Qarzdan", ["uz-cyrl"] = "Қарздан", ["ru"] = "Из долга", ["en"] = "From debt" },
        ["no_charge"] = new() { ["uz-latn"] = "Hisobsiz", ["uz-cyrl"] = "Ҳисобсиз", ["ru"] = "Без оплаты", ["en"] = "No charge" }
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
        "Transfer" => Get("transfer", lang),
        "Bank" => Get("bank", lang),
        _ => method
    };
}
