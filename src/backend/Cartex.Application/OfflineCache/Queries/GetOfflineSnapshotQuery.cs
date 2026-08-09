using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Application.Stocks.Queries;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.OfflineCache;
using Microsoft.EntityFrameworkCore;
using SharedStockOnHandDto = Cartex.Shared.Models.Stocks.StockOnHandDto;

namespace Cartex.Application.OfflineCache.Queries;

public sealed record GetOfflineSnapshotQuery(
    long LeaseId,
    long Epoch,
    string LeaseToken) : ICommand<OfflineSnapshotDto>;

public sealed class GetOfflineSnapshotQueryHandler(
    IApplicationDbContext db,
    ISettingsService settings,
    ICurrentUser currentUser,
    ISender sender)
    : IRequestHandler<GetOfflineSnapshotQuery, OfflineSnapshotDto>
{
    public async Task<OfflineSnapshotDto> Handle(GetOfflineSnapshotQuery request, CancellationToken cancellationToken)
    {
        var lease = await OfflineLeaseSecurity.RequireActiveAsync(db, currentUser,
            request.LeaseId, request.Epoch, request.LeaseToken, true, cancellationToken);
        var policy = await settings.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken)
            ?? new SalesPolicySettings();
        var baseCode = await db.Businesses
            .Where(x => x.Id == lease.BusinessId)
            .Select(x => x.Currency)
            .SingleAsync(cancellationToken);

        var products = new List<SharedStockOnHandDto>();
        for (var page = 1; ; page++)
        {
            var chunk = await sender.Send(new GetStockOnHandQuery(
                lease.WarehouseId, Page: page, PageSize: 1000, ForSale: true), cancellationToken);
            products.AddRange(chunk.Items.Select(x => new SharedStockOnHandDto(
                x.VariantId, x.ProductName, x.CategoryId, x.CategoryName, x.UnitName,
                x.Dimension, x.Quantity, x.SellingPrice, x.NearestExpiry, x.ImageUrl,
                x.DiscountPct, x.Code, x.Barcodes, x.AllowsAmountEntry,
                x.QuantityStep, x.AllowsFractional)));
            if (chunk.Items.Count < 1000) break;
        }

        var variantIds = products.Select(x => x.VariantId).ToHashSet();
        var barcodes = variantIds.Count == 0
            ? []
            : await db.Barcodes.AsNoTracking()
                .Where(x => variantIds.Contains(x.VariantId))
                .Select(x => new OfflineBarcodeDto(x.VariantId, x.Code, x.PackQty))
                .ToListAsync(cancellationToken);

        var customers = await db.Customers.AsNoTracking()
            .OrderBy(x => x.FullName)
            .Select(x => new OfflineCustomerDto(
                x.Id,
                x.FullName,
                x.Phone,
                x.CardBarcode,
                x.DiscountPct,
                x.Accounts.Where(a => a.Type == AccountType.Debt).Sum(a =>
                    a.Balance * (a.Currency == baseCode
                        ? 1m
                        : db.ExchangeRates.Where(r => r.Code == a.Currency)
                            .OrderByDescending(r => r.EffectiveAt)
                            .Select(r => r.Rate)
                            .FirstOrDefault())),
                x.CreditLimit))
            .ToListAsync(cancellationToken);

        var roles = await db.ParticipantRoleDefinitions.AsNoTracking()
            .Where(x => x.BusinessId == lease.BusinessId && x.IsEnabled && x.AppliesToCart)
            .OrderBy(x => x.SortOrder)
            .Select(x => new OfflineParticipantRoleDto(
                x.Id, x.Key, x.SingularLabel, x.IsRequired,
                x.CanEqualBuyer, x.MaxCount, x.SortOrder))
            .ToListAsync(cancellationToken);
        var partners = await db.PartnerProfiles.AsNoTracking()
            .Where(x => x.Party.BusinessId == lease.BusinessId && x.IsEnabled)
            .OrderBy(x => x.Party.FullName)
            .Select(x => new OfflinePartnerDto(
                x.Id, x.PartyId, x.PartnerCode, x.Party.FullName,
                x.Party.Phone, x.Party.CustomerProfile == null
                    ? null
                    : (long?)x.Party.CustomerProfile.Id))
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        lease.LastHeartbeatAt = now;
        lease.Version++;
        await db.SaveChangesAsync(cancellationToken);

        return new OfflineSnapshotDto(baseCode, now, products, barcodes, customers,
            policy.AllowDebtSales, policy.AllowInsufficientStockSales,
            lease.Id, lease.Epoch, now.Ticks, roles, partners);
    }
}
