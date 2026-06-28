using Cartex.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Sales.Queries;

public record GetReceiptByTokenQuery(string Token) : IRequest<ReceiptDto?>;

public record ReceiptItemDto(string ProductName, decimal Quantity, decimal UnitPrice, decimal LineTotal);

public record ReceiptDto(
    string ReceiptToken,
    string BusinessName,
    string BranchName,
    string? BranchAddress,
    DateTime SaleDate,
    decimal TotalAmount,
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidBonus,
    decimal DebtAmount,
    List<ReceiptItemDto> Items);

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
                sale.CreatedAt,
                sale.TotalAmount,
                sale.PaidCash,
                sale.PaidCard,
                sale.PaidBonus,
                sale.DebtAmount,
                sale.Items.Select(i => new ReceiptItemDto(i.Product.Name, i.Quantity, i.UnitPrice, i.Quantity * i.UnitPrice)).ToList()))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
