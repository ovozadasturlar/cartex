using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Stocks.Queries;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.OfflineCache.Queries;

public record OfflineBarcodeDto(long VariantId, string Code, decimal PackQty);

public record OfflineCustomerDto(long Id, string FullName, string? Phone, string? CardBarcode, decimal DiscountPct, decimal DebtBalance, decimal CreditLimit);

public record OfflineSnapshotDto(string BaseCurrency, DateTime ServerTime, List<StockOnHandDto> Products, List<OfflineBarcodeDto> Barcodes, List<OfflineCustomerDto> Customers, bool AllowDebtSales = true);

public record GetOfflineSnapshotQuery(long WarehouseId, string DeviceId) : IRequest<OfflineSnapshotDto>;

public sealed class GetOfflineSnapshotQueryHandler(IApplicationDbContext db, ISettingsService settings, ISender sender)
    : IRequestHandler<GetOfflineSnapshotQuery, OfflineSnapshotDto>
{
    public async Task<OfflineSnapshotDto> Handle(GetOfflineSnapshotQuery request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<OfflineCacheSettings>(SettingKeys.OfflineCache, cancellationToken);
        if (cfg?.DeviceId != request.DeviceId)
            throw new BusinessRuleException("Oflayn kesh bu qurilmaga berilmagan.");

        var policy = await settings.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken) ?? new SalesPolicySettings();

        var baseCode = await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);

        var products = new List<StockOnHandDto>();
        for (var page = 1; ; page++)
        {
            var chunk = await sender.Send(new GetStockOnHandQuery(request.WarehouseId, Page: page, PageSize: 1000), cancellationToken);
            products.AddRange(chunk.Items);
            if (chunk.Items.Count < 1000) break;
        }

        var variantIds = products.Select(p => p.VariantId).ToHashSet();
        var barcodes = (await db.Barcodes
                .Where(b => variantIds.Contains(b.VariantId))
                .Select(b => new OfflineBarcodeDto(b.VariantId, b.Code, b.PackQty))
                .ToListAsync(cancellationToken));

        var customers = await db.Customers
            .OrderBy(c => c.FullName)
            .Select(c => new OfflineCustomerDto(
                c.Id,
                c.FullName,
                c.Phone,
                c.CardBarcode,
                c.DiscountPct,
                c.Accounts.Where(a => a.Type == AccountType.Debt).Sum(a => a.Balance * (a.Currency == baseCode ? 1m
                    : db.ExchangeRates.Where(r => r.Code == a.Currency).OrderByDescending(r => r.EffectiveAt).Select(r => r.Rate).FirstOrDefault())),
                c.CreditLimit))
            .ToListAsync(cancellationToken);

        return new OfflineSnapshotDto(baseCode, DateTime.UtcNow, products, barcodes, customers, policy.AllowDebtSales);
    }
}
