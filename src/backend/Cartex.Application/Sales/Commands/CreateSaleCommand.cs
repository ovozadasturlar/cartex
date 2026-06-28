using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Application.Common.Finance;

namespace Cartex.Application.Sales.Commands;

public record CreateSaleItemDto(long ProductId, long StockId, decimal Quantity, decimal UnitPrice);

public record CreateSaleCommand(
    long WarehouseId,
    long? CustomerId,
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidBonus,
    List<CreateSaleItemDto> Items) : ICommand<long>;

public sealed class CreateSaleCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ILedgerService ledger) : IRequestHandler<CreateSaleCommand, long>
{
    public async Task<long> Handle(CreateSaleCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");

        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
            ?? throw new NotFoundException("Warehouse not found.");

        var totalAmount = request.Items.Sum(i => i.Quantity * i.UnitPrice);
        var debtAmount = Math.Max(0, totalAmount - request.PaidCash - request.PaidCard - request.PaidBonus);

        if ((request.PaidBonus > 0 || debtAmount > 0) && request.CustomerId is null)
            throw new BusinessRuleException("Bonus to'lov yoki qarz uchun mijoz tanlanishi shart.");

        if (request.CustomerId is not null && request.PaidBonus > 0)
        {
            var bonusAccount = await ledger.FindCustomerAccountAsync(request.CustomerId.Value, AccountType.Bonus, cancellationToken);
            if ((bonusAccount?.Balance ?? 0) < request.PaidBonus)
                throw new BusinessRuleException("Bonus balansi yetarli emas.");
        }

        var sale = new Sale
        {
            BranchId = warehouse.BranchId,
            WarehouseId = request.WarehouseId,
            UserId = userId,
            CustomerId = request.CustomerId,
            TotalAmount = totalAmount,
            PaidCash = request.PaidCash,
            PaidCard = request.PaidCard,
            PaidBonus = request.PaidBonus,
            DebtAmount = debtAmount,
            Status = SaleStatus.Completed
        };

        foreach (var item in request.Items)
        {
            var stock = await db.Stocks.FirstOrDefaultAsync(s => s.Id == item.StockId, cancellationToken)
                ?? throw new NotFoundException($"Stock {item.StockId} not found.");

            sale.Items.Add(new SaleItem
            {
                ProductId = item.ProductId,
                StockId = item.StockId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                PurchasePrice = stock.PurchasePrice
            });

            stock.Quantity -= item.Quantity;
        }

        db.Sales.Add(sale);

        await PostLedgerAsync(request, sale, warehouse.BranchId, totalAmount, debtAmount, userId, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return sale.Id;
    }

    private async Task PostLedgerAsync(CreateSaleCommand request, Sale sale, long branchId, decimal totalAmount, decimal debtAmount, long userId, CancellationToken cancellationToken)
    {
        if (request.PaidCash > 0)
        {
            var cash = await ledger.BranchAccountAsync(branchId, AccountType.Cash, cancellationToken);
            ledger.Post(OperationType.Sale, request.PaidCash, null, cash, userId).Sale = sale;
        }

        if (request.PaidCard > 0)
        {
            var card = await ledger.BranchAccountAsync(branchId, AccountType.Card, cancellationToken);
            ledger.Post(OperationType.Sale, request.PaidCard, null, card, userId).Sale = sale;
        }

        if (request.CustomerId is null)
            return;

        var customerId = request.CustomerId.Value;

        if (request.PaidBonus > 0)
        {
            var bonus = await ledger.CustomerAccountAsync(customerId, AccountType.Bonus, cancellationToken);
            ledger.Post(OperationType.BonusSpend, request.PaidBonus, bonus, null, userId).Sale = sale;
        }

        if (debtAmount > 0)
        {
            var debt = await ledger.CustomerAccountAsync(customerId, AccountType.Debt, cancellationToken);
            ledger.Post(OperationType.DebtCharge, debtAmount, null, debt, userId).Sale = sale;
        }

        var rate = await db.Businesses.Select(b => b.CashbackRate).FirstOrDefaultAsync(cancellationToken);
        var cashback = rate > 0 ? totalAmount * rate / 100 : 0;
        if (cashback > 0)
        {
            var bonus = await ledger.CustomerAccountAsync(customerId, AccountType.Bonus, cancellationToken);
            ledger.Post(OperationType.Cashback, cashback, null, bonus, userId).Sale = sale;
        }
    }
}

public sealed class CreateSaleCommandValidator : AbstractValidator<CreateSaleCommand>
{
    public CreateSaleCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleFor(x => x.PaidCash).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PaidCard).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PaidBonus).GreaterThanOrEqualTo(0);
    }
}
