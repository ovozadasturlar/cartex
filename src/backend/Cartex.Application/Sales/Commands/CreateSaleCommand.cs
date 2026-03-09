using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;

namespace Cartex.Application.Sales.Commands;

public record CreateSaleItemDto(long ProductId, long StockId, decimal Quantity, decimal UnitPrice);

public record CreateSaleCommand(
    long WarehouseId,
    long UserId,
    long? CustomerId,
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidBonus,
    List<CreateSaleItemDto> Items) : IRequest<long>;

public sealed class CreateSaleCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateSaleCommand, long>
{
    public async Task<long> Handle(CreateSaleCommand request, CancellationToken cancellationToken)
    {
        var totalAmount = request.Items.Sum(i => i.Quantity * i.UnitPrice);
        var debtAmount = totalAmount - request.PaidCash - request.PaidCard - request.PaidBonus;

        var sale = new Sale
        {
            WarehouseId = request.WarehouseId,
            UserId = request.UserId,
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
                ?? throw new Exception($"Stock {item.StockId} not found.");

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
            var warehouse = await db.Warehouses
                .Include(w => w.Shop)
                .FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken);

            if (warehouse is not null && warehouse.Shop.CashbackRate > 0)
            {
                var customer = await db.Customers
                    .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);

                if (customer is not null)
                    customer.CashbackBalance += totalAmount * warehouse.Shop.CashbackRate / 100;
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
