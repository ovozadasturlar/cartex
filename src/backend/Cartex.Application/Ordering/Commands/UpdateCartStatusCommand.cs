using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Ordering.Commands;

public record UpdateCartStatusCommand(string Code, CartStatus Status) : ICommand<Unit>;

public sealed class UpdateCartStatusCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateCartStatusCommand, Unit>
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

        if (!Allowed.TryGetValue(cart.Status, out var next) || !next.Contains(request.Status))
            throw new BusinessRuleException("Bu holatga o'tish mumkin emas.");

        cart.Status = request.Status;
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
