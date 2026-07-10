using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Supplies.Queries;

public record SupplyItemDto(long VariantId, string ProductName, string UnitName, decimal Quantity, long? UnitId, decimal PackSize, decimal PurchasePrice, DateOnly? ExpiredAt);

public record SupplyDetailDto(
    long Id,
    DateOnly SupplyDate,
    long SupplierId,
    string SupplierName,
    string WarehouseName,
    string UserName,
    decimal TotalAmount,
    decimal PaidCash,
    decimal PaidCard,
    string Currency,
    decimal Rate,
    List<SupplyItemDto> Items);

public record GetSupplyByIdQuery(long Id) : IRequest<SupplyDetailDto?>;

public sealed class GetSupplyByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetSupplyByIdQuery, SupplyDetailDto?>
{
    public async Task<SupplyDetailDto?> Handle(GetSupplyByIdQuery request, CancellationToken cancellationToken)
    {
        var supply = await db.Supplies
            .Include(s => s.Supplier)
            .Include(s => s.Warehouse)
            .Include(s => s.User)
            .Include(s => s.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.Product).ThenInclude(p => p.Unit)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (supply is null)
            return null;

        var expiries = (await db.Stocks
                .Where(st => st.SupplyId == supply.Id)
                .OrderBy(st => st.Id)
                .Select(st => new { st.VariantId, st.ExpiredAt })
                .ToListAsync(cancellationToken))
            .GroupBy(st => st.VariantId)
            .ToDictionary(g => g.Key, g => new Queue<DateOnly?>(g.Select(x => x.ExpiredAt)));

        var payments = await db.Transactions
            .Where(t => t.SupplyId == supply.Id && t.OperationType == OperationType.SupplyPay && t.FromAccount != null)
            .GroupBy(t => t.FromAccount!.Type)
            .Select(g => new { Type = g.Key, Amount = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken);

        var items = supply.Items
            .OrderBy(i => i.Id)
            .Select(i => new SupplyItemDto(
                i.VariantId,
                i.Variant.Product.Name,
                i.Variant.Product.Unit.Name,
                i.Quantity,
                i.UnitId,
                i.PackSize,
                i.PurchasePrice,
                expiries.TryGetValue(i.VariantId, out var q) && q.Count > 0 ? q.Dequeue() : null))
            .ToList();

        return new SupplyDetailDto(
            supply.Id,
            supply.SupplyDate,
            supply.SupplierId,
            supply.Supplier.Name,
            supply.Warehouse.Name,
            supply.User.FullName,
            supply.TotalAmount,
            payments.FirstOrDefault(p => p.Type == AccountType.Cash)?.Amount ?? 0,
            payments.FirstOrDefault(p => p.Type == AccountType.Card)?.Amount ?? 0,
            supply.Currency,
            supply.Rate,
            items);
    }
}
