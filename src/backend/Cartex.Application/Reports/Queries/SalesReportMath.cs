namespace Cartex.Application.Reports.Queries;

internal static class SalesReportMath
{
    public static decimal DiscountRate(decimal saleGross, decimal discount) =>
        saleGross > 0 ? discount / saleGross : 0m;

    public static decimal NetRevenue(decimal quantity, decimal returnedQuantity, decimal unitPrice, decimal discountRate) =>
        (quantity - returnedQuantity) * unitPrice * (1 - discountRate);

    public static decimal Profit(decimal quantity, decimal returnedQuantity, decimal unitPrice, decimal purchasePrice, decimal discountRate) =>
        NetRevenue(quantity, returnedQuantity, unitPrice, discountRate) - (quantity - returnedQuantity) * purchasePrice;
}
