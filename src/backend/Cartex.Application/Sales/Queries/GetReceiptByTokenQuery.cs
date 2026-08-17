using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using Cartex.Shared.Models.Sales;

namespace Cartex.Application.Sales.Queries;

public record GetReceiptByTokenQuery(string Token) : IRequest<ReceiptDto?>;

public sealed class GetReceiptByTokenQueryHandler(IApplicationDbContext db) : IRequestHandler<GetReceiptByTokenQuery, ReceiptDto?>
{
    public async Task<ReceiptDto?> Handle(GetReceiptByTokenQuery request, CancellationToken cancellationToken)
    {
        var receipt = await (
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
                sale.Items.Select(i => new ReceiptItemDto(i.Variant.Product.Name, i.Quantity, i.Variant.Product.Unit.ShortName, i.UnitPrice, i.Quantity * i.UnitPrice, i.DiscountAmount)).ToList(),
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
                sale.Note,
                sale.Status.ToString()))
            .FirstOrDefaultAsync(cancellationToken);

        // A line can be filled from several stock batches, but the customer should still see
        // one row per product with everything that came off it in a single figure.
        return receipt is null
            ? null
            : receipt with
            {
                Items = [.. receipt.Items
                    .GroupBy(i => (i.ProductName, i.UnitName, i.UnitPrice))
                    .Select(g => new ReceiptItemDto(g.Key.ProductName, g.Sum(i => i.Quantity), g.Key.UnitName,
                        g.Key.UnitPrice, g.Sum(i => i.LineTotal), g.Sum(i => i.DiscountAmount)))]
            };
    }
}
