using Cartex.Application.Common.Interfaces;
using Cartex.Application.Sales.Commands;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Ordering.Commands;

public record CheckoutCartCommand(string Code, decimal PaidCash, decimal PaidCard, decimal PaidBonus, string? IdempotencyKey = null) : ICommand<long>;

public sealed class CheckoutCartCommandHandler(IApplicationDbContext db, ISender sender, ICurrentUser currentUser, ICartNotifier notifier) : IRequestHandler<CheckoutCartCommand, long>
{
    public async Task<long> Handle(CheckoutCartCommand request, CancellationToken cancellationToken)
    {
        var completed = await db.ExecuteInTransactionAsync<(long SaleId, string? CartKind)>(async () =>
        {
            var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim();
            if (idempotencyKey is not null)
            {
                var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
                var existingSaleId = await db.Sales
                    .Where(s => s.UserId == userId && s.IdempotencyKey == idempotencyKey)
                    .Select(s => (long?)s.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (existingSaleId is not null)
                    return (existingSaleId.Value, CartKind: (string?)null);
            }

            var cart = await db.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.AggregateCode == request.Code, cancellationToken)
                ?? throw new NotFoundException("Cart not found.");

            if (cart.Status is CartStatus.CheckedOut or CartStatus.Cancelled)
                throw new BusinessRuleException("Savatcha allaqachon yakunlangan yoki bekor qilingan.");

            var result = await sender.Send(new CreateSaleCommand(
                cart.WarehouseId,
                cart.CustomerId,
                request.PaidCash,
                request.PaidCard,
                request.PaidBonus,
                cart.Items.Select(i => new CreateSaleItemDto(i.VariantId, i.Quantity)).ToList(),
                IdempotencyKey: idempotencyKey), cancellationToken);

            cart.Status = CartStatus.CheckedOut;
            await db.SaveChangesAsync(cancellationToken);

            return (result.SaleId, CartKind: cart.Kind.ToString());
        }, cancellationToken);

        if (completed.CartKind is not null)
            await notifier.CartsChangedAsync(completed.CartKind, cancellationToken);
        return completed.SaleId;
    }
}
