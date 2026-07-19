using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Ordering.Commands;

public record UpdateCartStatusCommand(string Code, CartStatus Status) : ICommand<Unit>;

public sealed class UpdateCartStatusCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, ICartNotifier notifier) : IRequestHandler<UpdateCartStatusCommand, Unit>
{
    private static readonly Dictionary<CartStatus, CartStatus[]> Allowed = new()
    {
        [CartStatus.Open] = [CartStatus.Confirmed, CartStatus.Cancelled],
        [CartStatus.Confirmed] = [CartStatus.Ready, CartStatus.CheckedOut, CartStatus.Cancelled],
        [CartStatus.Ready] = [CartStatus.CheckedOut, CartStatus.Cancelled],
    };

    public async Task<Unit> Handle(UpdateCartStatusCommand request, CancellationToken cancellationToken)
    {
        var cart = await db.Carts.FirstOrDefaultAsync(c => c.AggregateCode == request.Code, cancellationToken)
            ?? throw new NotFoundException("Cart not found.");

        if (!currentUser.HasPermission(AppPermissions.Sales.Create) &&
            (request.Status != CartStatus.Cancelled || cart.CreatedBy != currentUser.UserId || cart.Status != CartStatus.Open))
            throw new ForbiddenException("Faqat o'zingiz yig'gan ochiq savatni bekor qila olasiz.");

        if (!Allowed.TryGetValue(cart.Status, out var next) || !next.Contains(request.Status))
            throw new BusinessRuleException("Bu holatga o'tish mumkin emas.");

        if (request.Status == CartStatus.Confirmed)
        {
            var now = DateTime.UtcNow;
            var claimed = await db.Carts
                .Where(c => c.Id == cart.Id && c.Status == CartStatus.Open)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Status, CartStatus.Confirmed)
                    .SetProperty(c => c.UpdatedAt, now)
                    .SetProperty(c => c.UpdatedBy, currentUser.UserId), cancellationToken);
            if (claimed == 0)
                throw new BusinessRuleException("Savat allaqachon olingan.");
            await notifier.CartsChangedAsync(cart.Kind.ToString(), cancellationToken);
            return Unit.Value;
        }

        cart.Status = request.Status;
        await db.SaveChangesAsync(cancellationToken);
        await notifier.CartsChangedAsync(cart.Kind.ToString(), cancellationToken);
        return Unit.Value;
    }
}
