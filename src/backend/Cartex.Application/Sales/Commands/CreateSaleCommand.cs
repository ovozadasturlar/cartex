using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;

namespace Cartex.Application.Sales.Commands;

public record CreateSaleItemDto(long ProductId, long StockId, decimal Quantity, decimal UnitPrice);

public record CreateSaleCommand(
    long WarehouseId,
    long? CustomerId,
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidBonus,
    List<CreateSaleItemDto> Items) : IRequest<long>;

public sealed class CreateSaleCommandHandler(IApplicationDbContext db, ICurrentUser currentUser) : IRequestHandler<CreateSaleCommand, long>
{
    public async Task<long> Handle(CreateSaleCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");

        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
            ?? throw new NotFoundException("Warehouse not found.");

        var totalAmount = request.Items.Sum(i => i.Quantity * i.UnitPrice);
        var debtAmount = totalAmount - request.PaidCash - request.PaidCard - request.PaidBonus;

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

        db.Sales.Add(sale);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var item in request.Items)
        {
            var stock = await db.Stocks.FirstOrDefaultAsync(s => s.Id == item.StockId, cancellationToken)
                ?? throw new NotFoundException($"Stock {item.StockId} not found.");

            db.SaleItems.Add(new SaleItem
            {
                SaleId = sale.Id,
                ProductId = item.ProductId,
                StockId = item.StockId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                PurchasePrice = stock.PurchasePrice
            });

            stock.Quantity -= item.Quantity;
        }

        if (request.CustomerId is not null)
        {
            var cashbackRate = await db.Businesses.Select(b => b.CashbackRate).FirstOrDefaultAsync(cancellationToken);

            if (cashbackRate > 0)
            {
                var customer = await db.Customers
                    .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);

                if (customer is not null)
                    customer.CashbackBalance += totalAmount * cashbackRate / 100;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return sale.Id;
    }
}

public sealed class CreateSaleCommandValidator : AbstractValidator<CreateSaleCommand>
{
    public CreateSaleCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
    }
}
