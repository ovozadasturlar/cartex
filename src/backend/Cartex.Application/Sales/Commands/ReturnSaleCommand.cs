using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Application.Common.Finance;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Sales.Commands;

public record ReturnLineDto(long SaleItemId, decimal Quantity, bool Restock, string? Reason);

public record ReturnSaleCommand(long SaleId, List<ReturnLineDto> Lines) : ICommand<Unit>;

public sealed class ReturnSaleCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, ILedgerService ledger, ISettingsService settingsService, IAuditService audit)
    : IRequestHandler<ReturnSaleCommand, Unit>
{
    public async Task<Unit> Handle(ReturnSaleCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");

        var sale = await db.Sales
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == request.SaleId, cancellationToken)
            ?? throw new NotFoundException("Sale not found.");

        if (sale.Status == SaleStatus.Returned)
            throw new BusinessRuleException("Savdo allaqachon to'liq qaytarilgan.");

        var saleGross = sale.Items.Sum(i => i.Quantity * i.UnitPrice);
        decimal returnedGross = 0;
        var restock = new List<(long StockId, decimal Quantity)>();

        foreach (var line in request.Lines)
        {
            var item = sale.Items.FirstOrDefault(i => i.Id == line.SaleItemId)
                ?? throw new NotFoundException("Sale item not found.");
            if (line.Quantity > item.Quantity - item.ReturnedQuantity)
                throw new BusinessRuleException("Qaytariladigan miqdor qolgan miqdordan oshib ketdi.");

            item.ReturnedQuantity += line.Quantity;
            returnedGross += line.Quantity * item.UnitPrice;
            if (line.Restock)
                restock.Add((item.StockId, line.Quantity));
        }

        if (restock.Count > 0)
        {
            var stockIds = restock.Select(r => r.StockId).Distinct().ToList();
            var stocks = await db.Stocks.Where(s => stockIds.Contains(s.Id)).ToListAsync(cancellationToken);
            foreach (var (stockId, quantity) in restock)
                stocks.First(s => s.Id == stockId).Quantity += quantity;
        }

        var shiftId = await db.Shifts
            .Where(s => s.UserId == userId && s.BranchId == sale.BranchId && s.Status == ShiftStatus.Open)
            .Select(s => (long?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var fraction = saleGross > 0 ? returnedGross / saleGross : 0m;
        var isFullReturn = sale.Items.All(i => i.ReturnedQuantity >= i.Quantity);
        var discountRate = saleGross > 0 ? sale.DiscountAmount / saleGross : 0m;
        var refundedTotal = sale.RefundedCash + sale.RefundedCard + sale.RefundedBonus + sale.RefundedDebt;
        var refundValue = isFullReturn
            ? sale.TotalAmount - refundedTotal
            : Math.Round(returnedGross * (1 - discountRate), 2);

        await PostRefundAsync(sale, refundValue, fraction, userId, shiftId, cancellationToken);

        sale.Status = isFullReturn ? SaleStatus.Returned : SaleStatus.PartialReturn;

        audit.Add("return", "sales", sale.Id, new { returnedGross, request.Lines });

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    private async Task PostRefundAsync(Sale sale, decimal refundValue, decimal fraction, long userId, long? shiftId, CancellationToken cancellationToken)
    {
        void Post(OperationType type, decimal amount, Account? from, Account? to)
        {
            if (amount <= 0) return;
            var transaction = ledger.Post(type, amount, from, to, userId, shiftId);
            transaction.Sale = sale;
        }

        var remaining = Math.Min(refundValue,
            (sale.DebtAmount - sale.RefundedDebt) + (sale.PaidBonus - sale.RefundedBonus) +
            (sale.PaidCard - sale.RefundedCard) + (sale.PaidCash - sale.RefundedCash));

        decimal Take(decimal cap)
        {
            var take = Math.Min(remaining, Math.Max(0, cap));
            remaining -= take;
            return take;
        }

        var debtTake = Take(sale.DebtAmount - sale.RefundedDebt);
        var bonusTake = Take(sale.PaidBonus - sale.RefundedBonus);
        var cardTake = Take(sale.PaidCard - sale.RefundedCard);
        var cashTake = Take(sale.PaidCash - sale.RefundedCash);

        var policy = await settingsService.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken) ?? new SalesPolicySettings();
        if (cashTake > 0 && shiftId is null && policy.ShiftPolicy != "Off")
            throw new BusinessRuleException("Naqd qaytarish uchun ochiq smena talab qilinadi.");

        if (cardTake > 0)
        {
            var card = await ledger.BranchAccountAsync(sale.BranchId, AccountType.Card, cancellationToken);
            Post(OperationType.Sale, cardTake, card, null);
            sale.RefundedCard += cardTake;
        }

        if (cashTake > 0)
        {
            var cash = await ledger.BranchAccountAsync(sale.BranchId, AccountType.Cash, cancellationToken);
            Post(OperationType.Sale, cashTake, cash, null);
            sale.RefundedCash += cashTake;
        }

        if (sale.CustomerId is not { } customerId)
            return;

        if (debtTake > 0)
        {
            var debt = await ledger.CustomerAccountAsync(customerId, AccountType.Debt, cancellationToken, sale.DebtCurrency);
            var debtInCurrency = sale.DebtRate == 1m ? debtTake : Math.Round(debtTake / sale.DebtRate, 2);
            Post(OperationType.DebtCharge, debtInCurrency, debt, null);
            sale.RefundedDebt += debtTake;
        }

        if (bonusTake > 0)
        {
            var bonus = await ledger.CustomerAccountAsync(customerId, AccountType.Bonus, cancellationToken);
            Post(OperationType.BonusSpend, bonusTake, null, bonus);
            sale.RefundedBonus += bonusTake;
        }

        if (sale.CashbackEarned > 0)
        {
            var bonus = await ledger.CustomerAccountAsync(customerId, AccountType.Bonus, cancellationToken);
            var cashbackTake = Math.Min(
                Math.Min(sale.CashbackEarned - sale.RefundedCashback, Math.Round(sale.CashbackEarned * fraction, 2)),
                bonus.Balance);
            Post(OperationType.Cashback, cashbackTake, bonus, null);
            sale.RefundedCashback += Math.Max(0, cashbackTake);
        }
    }
}

public sealed class ReturnSaleCommandValidator : AbstractValidator<ReturnSaleCommand>
{
    public ReturnSaleCommandValidator()
    {
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).Must(l => l.Quantity > 0).WithMessage("Miqdor 0 dan katta bo'lishi kerak.");
    }
}
