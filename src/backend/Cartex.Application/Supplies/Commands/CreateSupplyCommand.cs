using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Domain.Measurement;
using Cartex.Application.Common.Finance;
using Cartex.Application.Products;

namespace Cartex.Application.Supplies.Commands;

/// <summary>
/// Bitta kirim qatori foydalanuvchi kiritgan ko'rinishda keladi: miqdor + qaysi birlikda (UnitId)
/// yoki qaysi qadoqda (PackId), narx esa PriceBasis bo'yicha o'sha birlik/qadoq uchun yoki saqlash
/// birligi uchun. Server hammasini saqlash birligiga o'zi keltiradi — qadoq hajmi bazadan olinadi.
/// UnitId va PackId birga kelmaydi.
/// </summary>
public record CreateSupplyItemDto(
    long VariantId,
    decimal Quantity,
    decimal PurchasePrice,
    DateOnly? ExpiredAt,
    long? UnitId = null,
    decimal? SellingPrice = null,
    long? PackId = null,
    SupplyPriceBasis PriceBasis = SupplyPriceBasis.PerEntry);

public record CreateSupplyCommand(
    long SupplierId,
    long WarehouseId,
    DateOnly SupplyDate,
    List<CreateSupplyItemDto> Items,
    decimal PaidCash = 0,
    decimal PaidCard = 0,
    string? Currency = null) : ICommand<long>;

public sealed class CreateSupplyCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, ILedgerService ledger, ICurrencyService currency, ISettingsService settingsService, IAuditService audit) : IRequestHandler<CreateSupplyCommand, long>
{
    public async Task<long> Handle(CreateSupplyCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");

        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
            ?? throw new NotFoundException("Warehouse not found.");

        var baseCode = await currency.BaseAsync(cancellationToken);
        var supplyCurrency = request.Currency ?? baseCode;
        if (supplyCurrency != baseCode && !await currency.IsMulticurrencyAsync(cancellationToken))
            throw new BusinessRuleException("Ko'p valyuta rejimi o'chirilgan.");
        var supplyRate = supplyCurrency == baseCode ? 1m : await currency.RateAsync(supplyCurrency, cancellationToken);

        var variantIds = request.Items.Select(i => i.VariantId).Distinct().ToList();
        var variants = await db.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .Select(v => new { v.Id, v.ProductId, v.Product.Unit })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var lineUnitIds = request.Items.Where(i => i.UnitId is not null).Select(i => i.UnitId!.Value).Distinct().ToList();
        var lineUnits = lineUnitIds.Count == 0
            ? []
            : await db.Units.Where(u => lineUnitIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, cancellationToken);

        var packIds = request.Items.Where(i => i.PackId is not null).Select(i => i.PackId!.Value).Distinct().ToList();
        var packs = packIds.Count == 0
            ? []
            : await db.ProductPacks.Where(p => packIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        // Miqdor va sotib olish narxi kiritilgan birlik/qadoqdan saqlash birligiga keltiriladi.
        // Sotish narxi esa hech qachon o'girilmaydi: do'kon tonna yoki qop bilan sotmaydi, u doim
        // saqlash birligida (mas. so'm/kg) belgilanadi.
        (decimal Quantity, decimal Price, decimal? SellingPrice, decimal PackSize) Resolve(CreateSupplyItemDto item)
        {
            if (!variants.TryGetValue(item.VariantId, out var variant))
                throw new NotFoundException("Mahsulot topilmadi.");

            if (item.UnitId is not null && item.PackId is not null)
                throw new BusinessRuleException("Bir qatorda birlik va qadoq birga tanlanmaydi.");

            Domain.Entities.Unit? entryUnit = null;
            if (item.UnitId is { } uid && !lineUnits.TryGetValue(uid, out entryUnit))
                throw new NotFoundException("Birlik topilmadi.");

            var packSize = 1m;
            if (item.PackId is { } packId)
            {
                if (!packs.TryGetValue(packId, out var pack))
                    throw new NotFoundException("Qadoq topilmadi.");
                if (pack.ProductId != variant.ProductId)
                    throw new BusinessRuleException("Qadoq boshqa mahsulotga tegishli.");
                if (pack.Kind == PackKind.Sale)
                    throw new BusinessRuleException("Bu qadoq faqat sotuv uchun — kirimda ishlatilmaydi.");
                packSize = pack.Size;
            }

            var normalized = UnitConversion.Normalize(
                item.Quantity, item.PurchasePrice, entryUnit, variant.Unit, packSize, item.PriceBasis);

            return (normalized.Quantity, normalized.Price, item.SellingPrice, packSize);
        }

        var lines = request.Items.Select(item => (item, resolved: Resolve(item))).ToList();

        var supply = new Supply
        {
            BranchId = warehouse.BranchId,
            SupplierId = request.SupplierId,
            WarehouseId = request.WarehouseId,
            UserId = userId,
            SupplyDate = request.SupplyDate,
            TotalAmount = lines.Sum(l => l.resolved.Quantity * l.resolved.Price),
            Currency = supplyCurrency,
            Rate = supplyRate
        };

        foreach (var (item, resolved) in lines)
        {
            supply.Items.Add(new SupplyItem
            {
                VariantId = item.VariantId,
                UnitId = item.UnitId,
                PackId = item.PackId,
                Quantity = resolved.Quantity,
                PurchasePrice = resolved.Price,
                PackSize = resolved.PackSize,
                EntryQuantity = item.Quantity,
                EntryPrice = item.PurchasePrice,
                PriceBasis = item.PriceBasis
            });

            db.Stocks.Add(new Stock
            {
                BranchId = warehouse.BranchId,
                VariantId = item.VariantId,
                WarehouseId = request.WarehouseId,
                Supply = supply,
                Quantity = resolved.Quantity,
                PurchasePrice = Math.Round(resolved.Price * supplyRate, 4),
                ExpiredAt = item.ExpiredAt
            });

            if (resolved.SellingPrice is { } sellingPrice)
                await ProductPriceWriter.UpsertAsync(db, item.VariantId, null, sellingPrice, cancellationToken, supplyCurrency);
        }

        db.Supplies.Add(supply);

        var totalBase = Math.Round(supply.TotalAmount * supplyRate, 2);
        if (request.PaidCash + request.PaidCard > totalBase)
            throw new BusinessRuleException("To'lov summasi ta'minot summasidan oshib ketdi.");

        long? shiftId = null;
        if (request.PaidCash > 0)
        {
            shiftId = await db.Shifts
                .Where(s => s.UserId == userId && s.BranchId == warehouse.BranchId && s.Status == ShiftStatus.Open)
                .Select(s => (long?)s.Id)
                .FirstOrDefaultAsync(cancellationToken);
            var policy = await settingsService.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken) ?? new SalesPolicySettings();
            if (shiftId is null && policy.ShiftPolicy != "Off")
                throw new BusinessRuleException("Naqd to'lov uchun ochiq smena talab qilinadi.");
        }

        var supplierDebt = await ledger.SupplierAccountAsync(request.SupplierId, AccountType.Debt, cancellationToken, supplyCurrency);
        ledger.Post(OperationType.DebtCharge, supply.TotalAmount, supplierDebt, null, userId, rate: supplyRate).Supply = supply;

        if (request.PaidCash > 0)
        {
            var cash = await ledger.BranchAccountAsync(warehouse.BranchId, AccountType.Cash, cancellationToken);
            PostPayment(request.PaidCash, cash);
        }

        if (request.PaidCard > 0)
        {
            var card = await ledger.BranchAccountAsync(warehouse.BranchId, AccountType.Card, cancellationToken);
            PostPayment(request.PaidCard, card);
        }

        void PostPayment(decimal amountBase, Cartex.Domain.Entities.Account account)
        {
            if (supplyCurrency == baseCode)
            {
                ledger.Post(OperationType.SupplyPay, amountBase, account, supplierDebt, userId, shiftId).Supply = supply;
                return;
            }
            ledger.Post(OperationType.SupplyPay, amountBase, account, null, userId, shiftId).Supply = supply;
            ledger.Post(OperationType.SupplyPay, Math.Round(amountBase / supplyRate, 2), null, supplierDebt, userId, shiftId, supplyRate).Supply = supply;
        }

        await db.SaveChangesAsync(cancellationToken);

        audit.Add("supply", "supplies", supply.Id, new { supply.SupplierId, supply.TotalAmount, request.PaidCash, request.PaidCard, Lines = request.Items.Count });
        await db.SaveChangesAsync(cancellationToken);

        return supply.Id;
    }
}

public sealed class CreateSupplyCommandValidator : AbstractValidator<CreateSupplyCommand>
{
    public CreateSupplyCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleFor(x => x.PaidCash).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PaidCard).GreaterThanOrEqualTo(0);

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.PurchasePrice).GreaterThanOrEqualTo(0);
            item.RuleFor(i => i.SellingPrice).GreaterThan(0).When(i => i.SellingPrice is not null);
        });
    }
}
