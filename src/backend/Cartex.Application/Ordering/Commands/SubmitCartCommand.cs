using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Ordering.Commands;

public record SubmitCartItemDto(long VariantId, decimal Quantity);

public record SubmitCartCommand(
    long WarehouseId,
    long? CustomerId,
    List<SubmitCartItemDto> Items,
    string? IdempotencyKey = null,
    string? Note = null,
    CartKind? Kind = null,
    decimal PaidCash = 0,
    decimal PaidCard = 0,
    decimal PaidBonus = 0) : ICommand<string>;

public sealed class SubmitCartCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, ICartNotifier notifier) : IRequestHandler<SubmitCartCommand, string>
{
    public async Task<string> Handle(SubmitCartCommand request, CancellationToken cancellationToken)
    {
        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim();
        if (idempotencyKey is not null)
        {
            var existing = await db.Carts
                .Where(c => c.IdempotencyKey == idempotencyKey)
                .Select(c => c.AggregateCode)
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
                return existing;
        }

        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
            ?? throw new NotFoundException("Warehouse not found.");

        var cart = new Cart
        {
            BranchId = warehouse.BranchId,
            WarehouseId = request.WarehouseId,
            CustomerId = request.CustomerId,
            AggregateCode = Guid.NewGuid().ToString("N"),
            IdempotencyKey = idempotencyKey,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            Kind = request.Kind ?? await ResolveKindAsync(cancellationToken),
            PaidCash = request.PaidCash,
            PaidCard = request.PaidCard,
            PaidBonus = request.PaidBonus
        };

        foreach (var item in request.Items)
            cart.Items.Add(new CartItem { VariantId = item.VariantId, Quantity = item.Quantity });

        db.Carts.Add(cart);
        await db.SaveChangesAsync(cancellationToken);
        await notifier.CartsChangedAsync(cart.Kind.ToString(), cancellationToken);

        return cart.AggregateCode;
    }

    private async Task<CartKind> ResolveKindAsync(CancellationToken cancellationToken)
    {
        var destination = await db.Users
            .Where(u => u.Id == currentUser.UserId)
            .Select(u => u.CartDestination
                ?? u.UserRoles.OrderByDescending(ur => ur.Role.Priority).Select(ur => ur.Role.CartDestination).FirstOrDefault())
            .FirstOrDefaultAsync(cancellationToken);
        return string.Equals(destination, "order", StringComparison.OrdinalIgnoreCase) ? CartKind.Order : CartKind.Queue;
    }
}

public sealed class SubmitCartCommandValidator : AbstractValidator<SubmitCartCommand>
{
    public SubmitCartCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).Must(i => i.Quantity > 0).WithMessage("Miqdor 0 dan katta bo'lishi kerak.");
    }
}
