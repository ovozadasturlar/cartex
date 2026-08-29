using System.Text.Json;

namespace Cartex.UI.Services;

public static class OfflineEventFormat
{
    public static string KindKey(string kind) => kind switch
    {
        "customer.payment.create" => "offline_kind_payment",
        "supply.create" => "offline_kind_supply",
        _ => "offline_kind_sale"
    };

    public static string? Summary(string kind, JsonElement root)
    {
        try
        {
            switch (kind)
            {
                case "customer.payment.create":
                    var amount = root.GetProperty("tenders").EnumerateArray()
                        .Sum(x => x.GetProperty("amount").GetDecimal());
                    return amount.ToString("N0");
                case "supply.create":
                {
                    var items = root.GetProperty("items");
                    var total = items.EnumerateArray()
                        .Sum(x => x.GetProperty("quantity").GetDecimal() * x.GetProperty("purchasePrice").GetDecimal());
                    return $"{items.GetArrayLength()} × • {total:N0}";
                }
                default:
                {
                    var total = root.GetProperty("paidCash").GetDecimal() + root.GetProperty("paidCard").GetDecimal();
                    return $"{root.GetProperty("items").GetArrayLength()} × • {total:N0}";
                }
            }
        }
        catch
        {
            return null;
        }
    }
}
