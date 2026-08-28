using Cartex.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Ordering.Commands;

public record UpdateCartStatusCommand(string Code, CartStatus Status, string? Reason = null) : ICommand<Unit>;

public sealed class UpdateCartStatusCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ICartNotifier notifier,
    IAuditService audit) : IRequestHandler<UpdateCartStatusCommand, Unit>
{
    private static readonly Dictionary<CartStatus, CartStatus[]> Allowed = new()
    {
        [CartStatus.Open] = [CartStatus.Confirmed, CartStatus.Cancelled],
        [CartStatus.Confirmed] = [CartStatus.Open, CartStatus.Ready, CartStatus.Cancelled],
        [CartStatus.Ready] = [CartStatus.Cancelled],
    };

    public async Task<Unit> Handle(UpdateCartStatusCommand request, CancellationToken cancellationToken)
    {
        var cart = await db.Carts.FirstOrDefaultAsync(c => c.AggregateCode == request.Code, cancellationToken)
            ?? throw new NotFoundException("Cart not found.");

        if (!currentUser.HasPermission(AppPermissions.Sales.Checkout) &&
            (request.Status != CartStatus.Cancelled || cart.CreatedBy != currentUser.UserId || cart.Status != CartStatus.Open))
            throw new ForbiddenException("Faqat o'zingiz yig'gan ochiq savatni bekor qila olasiz.");

        if (!Allowed.TryGetValue(cart.Status, out var next) || !next.Contains(request.Status))
            throw new BusinessRuleException("Bu holatga o'tish mumkin emas.");

        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        if (request.Status == CartStatus.Confirmed)
        {
            var previousStatus = cart.Status;
            var now = DateTime.UtcNow;
            var claimed = await db.Carts
                .Where(c => c.Id == cart.Id && c.Status == CartStatus.Open)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Status, CartStatus.Confirmed)
                    .SetProperty(c => c.ClaimedByUserId, userId)
                    .SetProperty(c => c.ClaimedAt, now)
                    .SetProperty(c => c.Version, c => c.Version + 1)
                    .SetProperty(c => c.UpdatedAt, now)
                    .SetProperty(c => c.UpdatedBy, userId), cancellationToken);
            if (claimed == 0)
                throw new BusinessRuleException("Savat allaqachon olingan.");
            audit.SetOutcome("cart.claimed", "carts", cart.Id,
                new { cart.AggregateCode, from = previousStatus, to = request.Status, ClaimedByUserId = userId },
                "Savat kassaga olindi", cart.BranchId);
            await db.RunAfterCommitAsync(() => notifier.CartsChangedAsync(cart.BranchId, cart.Kind.ToString(), cancellationToken));
            return Unit.Value;
        }

        if (cart.Status is CartStatus.Confirmed or CartStatus.Ready
            && cart.ClaimedByUserId != userId
            && !currentUser.HasPermission(AppPermissions.Sales.OverrideClaim))
            throw new ConflictException("Savat boshqa kassir tomonidan olingan.", "cart_claimed_by_another_user");

        var fromStatus = cart.Status;
        cart.Status = request.Status;
        cart.Version++;
        if (request.Status == CartStatus.Open)
        {
            cart.ClaimedByUserId = null;
            cart.ClaimedAt = null;
        }
        if (request.Status == CartStatus.Cancelled)
        {
            cart.CancelledByUserId = userId;
            cart.CancelledAt = DateTime.UtcNow;
            cart.CancellationReason = string.IsNullOrWhiteSpace(request.Reason)
                ? null
                : request.Reason.Trim();
        }
        await db.SaveChangesAsync(cancellationToken);
        audit.SetOutcome(request.Status == CartStatus.Cancelled ? "cart.cancelled" : "cart.status_changed", "carts", cart.Id,
            new { cart.AggregateCode, from = fromStatus, to = request.Status, cart.CancellationReason },
            request.Status == CartStatus.Cancelled ? "Savat bekor qilindi" : "Savat holati o'zgartirildi", cart.BranchId);
        await db.RunAfterCommitAsync(() => notifier.CartsChangedAsync(cart.BranchId, cart.Kind.ToString(), cancellationToken));
        return Unit.Value;
    }
}
