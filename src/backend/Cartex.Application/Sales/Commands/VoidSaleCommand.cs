using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Partners;
using Cartex.Application.Common.Sales;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Unit = Cartex.Application.Common.Messaging.Unit;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Sales.Commands;

/// <summary>
/// Reverses a posted sale with a storno instead of editing it, so stock, ledger,
/// cashback and partner rewards stay consistent and the correction leaves a trail.
/// </summary>
public sealed record VoidSaleCommand(long SaleId, string Reason) : ICommand<Unit>;

public sealed class VoidSaleCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ILedgerService ledger,
    ISaleCorrectionPolicy correctionPolicy,
    IPartnerRewardService partnerRewards,
    IAuditService audit) : IRequestHandler<VoidSaleCommand, Unit>
{
    public async Task<Unit> Handle(VoidSaleCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        if (!currentUser.HasPermission(AppPermissions.Sales.Void))
            throw new ForbiddenException("Savdoni bekor qilishga ruxsat yo'q.");

        var sale = await db.Sales
            .FromSqlInterpolated($"SELECT * FROM sales WHERE id = {request.SaleId} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sale not found.", "sale_not_found");
        if (!currentUser.CanAccessAllBranches && !currentUser.BranchIds.Contains(sale.BranchId))
            throw new NotFoundException("Sale not found.", "sale_not_found");

        if (sale.Status != SaleStatus.Completed)
            throw new BusinessRuleException("Faqat yakunlangan savdoni bekor qilish mumkin.", "sale_not_voidable");

        if (await db.CustomerReturnLines.AnyAsync(x => x.SaleId == sale.Id, cancellationToken))
            throw new BusinessRuleException(
                "Bu savdo bo'yicha qaytarish rasmiylashtirilgan — avval qaytarishni bekor qiling.", "sale_has_returns");

        if (await db.CustomerPaymentAllocations.AnyAsync(x => x.SaleId == sale.Id, cancellationToken))
            throw new BusinessRuleException(
                "Bu savdo qarzi bo'yicha to'lov qabul qilingan — savdoni bekor qilib bo'lmaydi.", "sale_has_payments");

        await correctionPolicy.EnsureCanCorrectAsync(sale, cancellationToken);

        await ReverseLedgerAsync(sale, userId, cancellationToken);
        await RestoreStockAsync(sale, cancellationToken);
        await partnerRewards.ReverseSaleAsync(sale, cancellationToken);

        sale.Status = SaleStatus.Voided;
        sale.VoidedAt = DateTime.UtcNow;
        sale.VoidReason = request.Reason.Trim();
        await db.SaveChangesAsync(cancellationToken);

        audit.SetOutcome("sale.voided", "sales", sale.Id, new
        {
            sale.ReceiptToken,
            sale.BranchId,
            sale.CustomerId,
            sale.ShiftId,
            sale.TotalAmount,
            sale.VoidReason
        }, "Savdo bekor qilindi (storno)", sale.BranchId);

        return Unit.Value;
    }

    // Posting the exact mirror of every transaction guarantees the ledger returns to its
    // pre-sale state regardless of which payment mix was used.
    private async Task ReverseLedgerAsync(Sale sale, long userId, CancellationToken cancellationToken)
    {
        var transactions = await db.Transactions
            .Where(x => x.SaleId == sale.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var original in transactions)
        {
            var from = original.ToAccountId is { } toId ? await ledger.AccountAsync(toId, cancellationToken) : null;
            var to = original.FromAccountId is { } fromId ? await ledger.AccountAsync(fromId, cancellationToken) : null;
            if (from is null && to is null) continue;

            var reversal = ledger.Post(original.OperationType, original.Amount, from, to,
                userId, original.ShiftId, original.Rate);
            reversal.SaleId = sale.Id;
            reversal.BranchId = original.BranchId;
            reversal.Description = $"VOID {sale.ReceiptToken}";
        }
    }

    private async Task RestoreStockAsync(Sale sale, CancellationToken cancellationToken)
    {
        var items = await db.SaleItems.Where(x => x.SaleId == sale.Id).ToListAsync(cancellationToken);
        var byStock = items.GroupBy(x => x.StockId).ToDictionary(x => x.Key, x => x.Sum(item => item.Quantity));
        if (byStock.Count == 0) return;

        var stockIds = byStock.Keys.ToArray();
        var stocks = await db.Stocks
            .FromSqlInterpolated($"SELECT * FROM stocks WHERE id = ANY({stockIds}) FOR UPDATE")
            .ToListAsync(cancellationToken);
        foreach (var stock in stocks)
            stock.Quantity += byStock[stock.Id];
    }
}

public sealed class VoidSaleCommandValidator : AbstractValidator<VoidSaleCommand>
{
    public VoidSaleCommandValidator()
    {
        RuleFor(x => x.SaleId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
