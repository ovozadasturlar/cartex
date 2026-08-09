using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Application.Common.Measurement;
using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Ordering.Commands;

public record UpdateCartItemsCommand(string Code, List<SubmitCartItemDto> Items, int? ExpectedVersion = null) : ICommand<Unit>;

public sealed class UpdateCartItemsCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ICartNotifier notifier,
    IQuantityPolicyService quantityPolicy,
    IAuditService audit) : IRequestHandler<UpdateCartItemsCommand, Unit>
{
    public async Task<Unit> Handle(UpdateCartItemsCommand request, CancellationToken cancellationToken)
    {
        var cart = await db.Carts
            .FromSqlInterpolated($"SELECT * FROM carts WHERE aggregate_code = {request.Code} FOR UPDATE")
            .Include(x => x.Items).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Cart not found.");

        if (cart.Status is not (CartStatus.Open or CartStatus.Confirmed))
            throw new BusinessRuleException("Bu savat endi tahrirlanmaydi.");
        if (!currentUser.HasPermission(AppPermissions.Sales.Create))
            throw new ForbiddenException("Savatni faqat kassir tahrirlashi mumkin.");
        if (cart.Status == CartStatus.Confirmed && cart.ClaimedByUserId != currentUser.UserId
            && !currentUser.HasPermission(AppPermissions.Sales.OverrideClaim))
            throw new ForbiddenException("Savat kassaga olingan.");
        if (request.ExpectedVersion.HasValue && cart.Version != request.ExpectedVersion)
            throw new ConflictException("Savat boshqa qurilmada o'zgartirilgan. Yangilab qayta urinib ko'ring.", "cart_version_conflict");
        if (request.Items.Count == 0)
        {
            cart.Status = CartStatus.Cancelled;
            cart.CancelledByUserId = currentUser.UserId;
            cart.CancelledAt = DateTime.UtcNow;
            cart.CancellationReason = "empty_cart";
            cart.Version++;
            await db.SaveChangesAsync(cancellationToken);
            audit.SetOutcome("cart.cancelled", "carts", cart.Id,
                new { cart.AggregateCode, reason = "empty_cart" }, "Bo'sh savat bekor qilindi", cart.BranchId);
            await notifier.CartsChangedAsync(cart.Kind.ToString(), cancellationToken);
            return Unit.Value;
        }

        if (request.Items.Select(x => x.VariantId).Distinct().Count() != request.Items.Count)
            throw new BusinessRuleException("Bir mahsulot varianti savatda takrorlanmasligi kerak.", "duplicate_cart_item");

        await quantityPolicy.ValidateAsync(
            request.Items.Select(x => (x.VariantId, x.Quantity)), cancellationToken);

        cart.Items.Clear();
        foreach (var item in request.Items)
        {
            cart.Items.Add(new CartItem { VariantId = item.VariantId, Quantity = item.Quantity });
        }
        cart.Version++;

        await db.SaveChangesAsync(cancellationToken);
        audit.SetOutcome("cart.items_updated", "carts", cart.Id,
            new { cart.AggregateCode, items = request.Items }, "Savatdagi mahsulotlar o'zgartirildi", cart.BranchId);
        await notifier.CartsChangedAsync(cart.Kind.ToString(), cancellationToken);
        return Unit.Value;
    }
}
