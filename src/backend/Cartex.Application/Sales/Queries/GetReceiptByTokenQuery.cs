using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Sales.Queries;

public record GetReceiptByTokenQuery(string Token) : IRequest<ReceiptDto?>;

public record ReceiptItemDto(string ProductName, decimal Quantity, string UnitName, decimal UnitPrice, decimal LineTotal);

public record ReceiptPaymentDto(string Method, string Currency, decimal Amount);

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
    string? Language = null);

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
                sale.Payments.Select(p => new ReceiptPaymentDto(p.Method.ToString(), p.Currency, p.Amount)).ToList(),
                sale.Id,
                sale.Customer != null ? sale.Customer.FullName : null,
                sale.Customer != null ? sale.Customer.PreferredLanguage : null))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
