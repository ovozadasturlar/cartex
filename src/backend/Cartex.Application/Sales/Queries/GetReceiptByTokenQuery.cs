using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace Cartex.Application.Sales.Queries;

public record GetReceiptByTokenQuery(string Token) : IRequest<ReceiptDto?>;

public record ReceiptItemDto(string ProductName, decimal Quantity, string UnitName, decimal UnitPrice, decimal LineTotal);

public record ReceiptPaymentDto(string Method, string Currency, decimal Amount, decimal Rate = 1m, decimal AmountBase = 0, bool IsForeign = false);

public record ReceiptDto(
    string ReceiptToken,
    string BusinessName,
    string BranchName,
    string? BranchAddress,
    string? BranchPhone,
    DateTime SaleDate,
    decimal TotalAmount,
    decimal DiscountAmount,
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidBonus,
    decimal DebtAmount,
    decimal ChangeAmount,
    decimal CashbackEarned,
    string UserName,
    List<ReceiptItemDto> Items,
    List<ReceiptPaymentDto> Payments,
    long SaleId = 0,
    string? CustomerName = null,
    string? CustomerPhone = null,
    string? CustomerEmail = null,
    string? Language = null,
    string? BusinessPhone = null,
    string? BusinessTelegram = null,
    string? BusinessWebsite = null,
    string? LogoImageKey = null,
    decimal CreditAmount = 0,
    string? BaseCurrency = null,
    decimal PaidAdvance = 0,
    string? MonochromeLogoImageKey = null,
    long? CustomerId = null,
    long? TradeCaseId = null,
    string? TradeCaseNumber = null,
    string? TradeCaseTitle = null)
{
    // Rendering-only data. It is populated only for PDF/image generation so the
    // normal receipt API remains lightweight and never serializes a base64 logo.
    [JsonIgnore]
    public byte[]? LogoBytes { get; init; }
}

public sealed class GetReceiptByTokenQueryHandler(IApplicationDbContext db) : IRequestHandler<GetReceiptByTokenQuery, ReceiptDto?>
{
    public async Task<ReceiptDto?> Handle(GetReceiptByTokenQuery request, CancellationToken cancellationToken)
    {
        return await (
            from sale in db.Sales
            where sale.ReceiptToken == request.Token
            join branch in db.Branches on sale.BranchId equals branch.Id
            join business in db.Businesses on branch.BusinessId equals business.Id
            select new ReceiptDto(
                sale.ReceiptToken,
                business.Name,
                branch.Name,
                branch.Address,
                branch.Phone,
                sale.CreatedAt,
                sale.TotalAmount,
                sale.DiscountAmount,
                sale.PaidCash + sale.ChangeAmount,
                sale.PaidCard,
                sale.PaidBonus,
                sale.DebtAmount,
                sale.ChangeAmount,
                sale.CashbackEarned,
                sale.User.FullName,
                sale.Items.Select(i => new ReceiptItemDto(i.Variant.Product.Name, i.Quantity, i.Variant.Product.Unit.ShortName, i.UnitPrice, i.Quantity * i.UnitPrice)).ToList(),
                sale.Payments.Select(p => new ReceiptPaymentDto(p.Method.ToString(), p.Currency, p.Amount, p.Rate, p.AmountBase, p.Currency != business.Currency)).ToList(),
                sale.Id,
                sale.Customer != null ? sale.Customer.FullName : null,
                sale.Customer != null ? sale.Customer.Phone : null,
                sale.Customer != null ? sale.Customer.Email : null,
                sale.Customer != null ? sale.Customer.PreferredLanguage : null,
                business.Phone,
                business.Telegram,
                business.Website,
                business.LogoImageKey,
                sale.CreditAmount,
                business.Currency,
                sale.PaidAdvance,
                business.MonochromeLogoImageKey,
                sale.CustomerId,
                sale.TradeCaseId,
                sale.TradeCase != null ? sale.TradeCase.CaseNumber : null,
                sale.TradeCase != null ? sale.TradeCase.Title : null))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
