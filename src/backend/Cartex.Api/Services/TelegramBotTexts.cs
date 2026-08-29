namespace Cartex.Api.Services;

public static class TelegramBotTexts
{
    public const string DefaultLanguage = "uz-latn";
    public static readonly string[] Languages = ["uz-latn", "uz-cyrl", "ru", "en"];

    public static readonly IReadOnlyDictionary<string, string> LanguageNames = new Dictionary<string, string>
    {
        ["uz-latn"] = "O'zbekcha",
        ["uz-cyrl"] = "Ўзбекча",
        ["ru"] = "Русский",
        ["en"] = "English"
    };

    private static readonly Dictionary<string, Dictionary<string, string>> Texts = new()
    {
        ["choose_language"] = new()
        {
            ["uz-latn"] = "Tilni tanlang:",
            ["uz-cyrl"] = "Тилни танланг:",
            ["ru"] = "Выберите язык:",
            ["en"] = "Choose a language:"
        },
        ["share_phone"] = new()
        {
            ["uz-latn"] = "Sizni tanishimiz uchun telefon raqamingizni ulashing.",
            ["uz-cyrl"] = "Сизни танишимиз учун телефон рақамингизни улашинг.",
            ["ru"] = "Поделитесь номером телефона, чтобы мы вас узнали.",
            ["en"] = "Share your phone number so we can identify you."
        },
        ["btn_share"] = new()
        {
            ["uz-latn"] = "📱 Raqamni ulashish",
            ["uz-cyrl"] = "📱 Рақамни улашиш",
            ["ru"] = "📱 Отправить номер",
            ["en"] = "📱 Share number"
        },
        ["share_button_required"] = new()
        {
            ["uz-latn"] = "Iltimos, pastdagi «📱 Raqamni ulashish» tugmasidan foydalaning. Qo'lda biriktirilgan kontakt qabul qilinmaydi.",
            ["uz-cyrl"] = "Илтимос, пастдаги «📱 Рақамни улашиш» тугмасидан фойдаланинг. Қўлда бириктирилган контакт қабул қилинмайди.",
            ["ru"] = "Пожалуйста, используйте кнопку «📱 Отправить номер» ниже. Вручную прикреплённый контакт не принимается.",
            ["en"] = "Please use the «📱 Share number» button below. A manually attached contact is not accepted."
        },
        ["linked"] = new()
        {
            ["uz-latn"] = "✅ Telegram ulandi! Cheklaringiz endi shu yerga keladi.",
            ["uz-cyrl"] = "✅ Telegram уланди! Чекларингиз энди шу ерга келади.",
            ["ru"] = "✅ Telegram подключён! Чеки теперь будут приходить сюда.",
            ["en"] = "✅ Telegram connected! Your receipts will arrive here."
        },
        ["not_found"] = new()
        {
            ["uz-latn"] = "Bu raqam tizimda topilmadi. Do'kon sotuvchisiga murojaat qiling.",
            ["uz-cyrl"] = "Бу рақам тизимда топилмади. Дўкон сотувчисига мурожаат қилинг.",
            ["ru"] = "Этот номер не найден. Обратитесь к продавцу магазина.",
            ["en"] = "This number was not found. Please contact the store staff."
        },
        ["menu"] = new()
        {
            ["uz-latn"] = "Kerakli bo'limni tanlang:",
            ["uz-cyrl"] = "Керакли бўлимни танланг:",
            ["ru"] = "Выберите раздел:",
            ["en"] = "Choose a section:"
        },
        ["btn_sales"] = new()
        {
            ["uz-latn"] = "🛍 Xaridlarim",
            ["uz-cyrl"] = "🛍 Харидларим",
            ["ru"] = "🛍 Мои покупки",
            ["en"] = "🛍 My purchases"
        },
        ["btn_balance"] = new()
        {
            ["uz-latn"] = "💰 Balans",
            ["uz-cyrl"] = "💰 Баланс",
            ["ru"] = "💰 Баланс",
            ["en"] = "💰 Balance"
        },
        ["btn_lang"] = new()
        {
            ["uz-latn"] = "🌐 Til",
            ["uz-cyrl"] = "🌐 Тил",
            ["ru"] = "🌐 Язык",
            ["en"] = "🌐 Language"
        },
        ["no_sales"] = new()
        {
            ["uz-latn"] = "Hozircha xaridlar yo'q.",
            ["uz-cyrl"] = "Ҳозирча харидлар йўқ.",
            ["ru"] = "Покупок пока нет.",
            ["en"] = "No purchases yet."
        },
        ["sales_header"] = new()
        {
            ["uz-latn"] = "Oxirgi xaridlaringiz:",
            ["uz-cyrl"] = "Охирги харидларингиз:",
            ["ru"] = "Ваши последние покупки:",
            ["en"] = "Your recent purchases:"
        },
        ["debt"] = new()
        {
            ["uz-latn"] = "Qarz",
            ["uz-cyrl"] = "Қарз",
            ["ru"] = "Долг",
            ["en"] = "Debt"
        },
        ["bonus"] = new()
        {
            ["uz-latn"] = "Bonus",
            ["uz-cyrl"] = "Бонус",
            ["ru"] = "Бонус",
            ["en"] = "Bonus"
        }
    };

    public static string Get(string key, string? lang) =>
        Texts[key].GetValueOrDefault(lang ?? DefaultLanguage) ?? Texts[key][DefaultLanguage];

    public static string? ActionFor(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        foreach (var lang in Languages)
        {
            if (text == Get("btn_sales", lang)) return "sales";
            if (text == Get("btn_balance", lang)) return "balance";
            if (text == Get("btn_lang", lang)) return "lang";
        }
        return null;
    }
}
