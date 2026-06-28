using Cartex.Application.Sales.Commands;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Ordering.Commands;

public record CheckoutCartCommand(string Code, decimal PaidCash, decimal PaidCard, decimal PaidBonus) : ICommand<long>;

public sealed class CheckoutCartCommandHandler(IApplicationDbContext db, ISender sender) : IRequestHandler<CheckoutCartCommand, long>
{
    public async Task<long> Handle(CheckoutCartCommand request, CancellationToken cancellationToken)
    {
        var cart = await db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.AggregateCode == request.Code, cancellationToken)
            ?? throw new NotFoundException("Cart not found.");

        if (cart.Status != CartStatus.Open)
            throw new BusinessRuleException("Savatcha allaqachon yakunlangan yoki bekor qilingan.");

        var saleId = await sender.Send(new CreateSaleCommand(
            cart.WarehouseId,
            cart.CustomerId,
            request.PaidCash,
            request.PaidCard,
            request.PaidBonus,
            cart.Items.Select(i => new CreateSaleItemDto(i.ProductId, i.Quantity)).ToList()), cancellationToken);

        cart.Status = CartStatus.CheckedOut;
        await db.SaveChangesAsync(cancellationToken);

        return saleId;
    }
}
