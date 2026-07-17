using Cartex.UI.Services;

namespace Cartex.UI.Models;

public sealed record PayMode(string Key, string Text)
{
    private static readonly string[] Keys = ["Cash", "Card", "Transfer", "Bank"];

    public static IReadOnlyList<PayMode> All() => [.. Keys.Select(k => new PayMode(k, TextFor(k)))];

    public static string TextFor(string key) => LocalizationManager.Instance[key switch
    {
        "Card" => "card",
        "Transfer" => "pay_transfer",
        "Bank" => "pay_bank",
        _ => "cash"
    }];
}
