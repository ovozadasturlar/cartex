using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Settings;
using Cartex.Application.Products;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Supplies.Commands;

public record UpdateSupplyCommand(
    long Id,
    long? SupplierId,
    long WarehouseId,
    DateOnly SupplyDate,
    List<CreateSupplyItemDto> Items,
    string? Currency = null) : ICommand<Unit>;

public sealed class UpdateSupplyCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ILedgerService ledger,
    ICurrencyService currency,
    ISettingsService settingsService,
    IAuditService audit) : IRequestHandler<UpdateSupplyCommand, Unit>
{
    public async Task<Unit> Handle(UpdateSupplyCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");

        if (request.SupplierId is null)
        {
            var supplierPolicy = await settingsService.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken) ?? new SalesPolicySettings();
            if (supplierPolicy.RequireSupplier)
                throw new BusinessRuleException("Ta'minotchi tanlash majburiy.");
        }

        var supply = await db.Supplies
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Ta'minot topilmadi.");

        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
            ?? throw new NotFoundException("Warehouse not found.");

        var stocks = await db.Stocks.Where(s => s.SupplyId == supply.Id).ToListAsync(cancellationToken);
        foreach (var group in supply.Items.GroupBy(i => i.VariantId))
        {
            var remaining = stocks.Where(s => s.VariantId == group.Key).Sum(s => s.Quantity);
            if (remaining < group.Sum(i => i.Quantity))
                throw new BusinessRuleException("Bu kirimdagi mahsulotlardan sotilgan yoki ishlatilgan — tahrirlab bo'lmaydi, bekor qilib qaytadan kiriting.");
        }

        var payments = await db.Transactions
            .Where(t => t.SupplyId == supply.Id && t.FromAccountId != null
                && (t.OperationType == OperationType.SupplyPay || t.OperationType == OperationType.DebtPay))
            .Select(t => t.Amount)
            .ToListAsync(cancellationToken);

        var paid = payments.Sum();
        if (paid > 0 && supply.SupplierId != request.SupplierId)
            throw new BusinessRuleException("To'lov biriktirilgan kirimda ta'minotchini o'zgartirib bo'lmaydi.");

        var baseCode = await currency.BaseAsync(cancellationToken);
        var supplyCurrency = request.Currency ?? baseCode;
        if (supplyCurrency != baseCode && !await currency.IsMulticurrencyAsync(cancellationToken))
            throw new BusinessRuleException("Ko'p valyuta rejimi o'chirilgan.");
        var supplyRate = supplyCurrency == baseCode ? 1m : await currency.RateAsync(supplyCurrency, cancellationToken);

        var resolver = await SupplyLineResolver.LoadAsync(db, request.Items, cancellationToken);
        var lines = request.Items.Select(item => (item, resolved: resolver.Resolve(item))).ToList();

        var total = lines.Sum(l => l.resolved.Quantity * l.resolved.Price);
        if (paid > Math.Round(total * supplyRate, 2))
            throw new BusinessRuleException("Yangi summa to'langan summadan kam bo'lishi mumkin emas.");

        var charges = await db.Transactions
            .Where(t => t.SupplyId == supply.Id && t.OperationType == OperationType.DebtCharge)
            .Include(t => t.FromAccount)
            .Include(t => t.ToAccount)
            .ToListAsync(cancellationToken);

        foreach (var charge in charges)
            ledger.Post(charge.OperationType, charge.Amount, charge.ToAccount, charge.FromAccount, userId, rate: charge.Rate).Supply = supply;

        foreach (var stock in stocks)
        {
            stock.IsDeleted = true;
            stock.DeletedAt = DateTime.UtcNow;
        }

        db.SupplyItems.RemoveRange(supply.Items);
        supply.Items.Clear();

        supply.SupplierId = request.SupplierId;
        supply.WarehouseId = request.WarehouseId;
        supply.BranchId = warehouse.BranchId;
        supply.SupplyDate = request.SupplyDate;
        supply.TotalAmount = total;
        supply.Currency = supplyCurrency;
        supply.Rate = supplyRate;

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

        if (request.SupplierId is { } sid)
        {
            var supplierDebt = await ledger.SupplierAccountAsync(sid, AccountType.Debt, cancellationToken, supplyCurrency);
            ledger.Post(OperationType.DebtCharge, total, supplierDebt, null, userId, rate: supplyRate).Supply = supply;
        }

        audit.Add("supplyedit", "supplies", supply.Id, new { supply.SupplierId, supply.TotalAmount, Lines = request.Items.Count });
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class UpdateSupplyCommandValidator : AbstractValidator<UpdateSupplyCommand>
{
    public UpdateSupplyCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty();

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.PurchasePrice).GreaterThanOrEqualTo(0);
            item.RuleFor(i => i.SellingPrice).GreaterThan(0).When(i => i.SellingPrice is not null);
        });
    }
}
