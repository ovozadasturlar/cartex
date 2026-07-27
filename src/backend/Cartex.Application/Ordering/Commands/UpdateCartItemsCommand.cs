using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Ordering.Commands;

public record UpdateCartItemsCommand(string Code, List<SubmitCartItemDto> Items) : ICommand<Unit>;

public sealed class UpdateCartItemsCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, ICartNotifier notifier) : IRequestHandler<UpdateCartItemsCommand, Unit>
{
    public async Task<Unit> Handle(UpdateCartItemsCommand request, CancellationToken cancellationToken)
    {
        var cart = await db.Carts.Include(x => x.Items).FirstOrDefaultAsync(x => x.AggregateCode == request.Code, cancellationToken)
            ?? throw new NotFoundException("Cart not found.");

        if (cart.Status is not (CartStatus.Open or CartStatus.Confirmed))
            throw new BusinessRuleException("Bu savat endi tahrirlanmaydi.");
        if (!currentUser.HasPermission(AppPermissions.Sales.Create))
            throw new ForbiddenException("Savatni faqat kassir tahrirlashi mumkin.");
        if (cart.Status == CartStatus.Confirmed && cart.UpdatedBy != currentUser.UserId)
            throw new ForbiddenException("Savat kassaga olingan.");
        if (request.Items.Count == 0)
        {
            cart.Status = CartStatus.Cancelled;
            await db.SaveChangesAsync(cancellationToken);
            await notifier.CartsChangedAsync(cart.Kind.ToString(), cancellationToken);
            return Unit.Value;
        }

        cart.Items.Clear();
        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
                throw new BusinessRuleException("Miqdor 0 dan katta bo'lishi kerak.");
            cart.Items.Add(new CartItem { VariantId = item.VariantId, Quantity = item.Quantity });
        }

        await db.SaveChangesAsync(cancellationToken);
        await notifier.CartsChangedAsync(cart.Kind.ToString(), cancellationToken);
        return Unit.Value;
    }
}
