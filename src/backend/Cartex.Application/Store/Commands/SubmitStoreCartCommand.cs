using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Domain.Events;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Store.Commands;

public record SubmitStoreCartItemDto(long VariantId, decimal Quantity);

public record SubmitStoreCartCommand(long WarehouseId, List<SubmitStoreCartItemDto> Items, string? IdempotencyKey = null) : ICommand<string>;

public sealed class SubmitStoreCartCommandHandler(IApplicationDbContext db, ICurrentCustomer currentCustomer)
    : IRequestHandler<SubmitStoreCartCommand, string>
{
    public async Task<string> Handle(SubmitStoreCartCommand request, CancellationToken cancellationToken)
    {
        var customerId = currentCustomer.CustomerId ?? throw new UnauthorizedAccessException("Not authenticated.");
        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim();

        if (idempotencyKey is not null)
        {
            var existing = await ExistingByKeyAsync(customerId, idempotencyKey, cancellationToken);
            if (existing is not null) return existing;
        }

        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId && w.IsOnline, cancellationToken)
            ?? throw new NotFoundException("Do'kon topilmadi.");

        var baseCode = await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);
        var variantIds = request.Items.Select(i => i.VariantId).Distinct().ToList();
        var inCatalog = await db.ProductVariants
            .Where(v => variantIds.Contains(v.Id)
                && v.Prices.Any(p => (p.WarehouseId == request.WarehouseId || p.WarehouseId == null) && p.Currency == baseCode))
            .CountAsync(cancellationToken);
        if (inCatalog != variantIds.Count)
            throw new NotFoundException("Mahsulot topilmadi.");

        var cart = new Cart
        {
            BranchId = warehouse.BranchId,
            WarehouseId = warehouse.Id,
            CustomerId = customerId,
            AggregateCode = Guid.NewGuid().ToString("N"),
            IdempotencyKey = idempotencyKey,
            Kind = CartKind.Order
        };

        foreach (var item in request.Items)
            cart.Items.Add(new CartItem { VariantId = item.VariantId, Quantity = item.Quantity });

        cart.RaiseDomainEvent(new CartSubmittedEvent(cart.AggregateCode, customerId, request.Items.Count, warehouse.Name));

        db.Carts.Add(cart);
        await db.SaveChangesAsync(cancellationToken);

        return cart.AggregateCode;
    }

    private Task<string?> ExistingByKeyAsync(long customerId, string key, CancellationToken cancellationToken) =>
        db.Carts
            .Where(c => c.CustomerId == customerId && c.IdempotencyKey == key)
            .Select(c => (string?)c.AggregateCode)
            .FirstOrDefaultAsync(cancellationToken);
}

public sealed class SubmitStoreCartCommandValidator : AbstractValidator<SubmitStoreCartCommand>
{
    public SubmitStoreCartCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).Must(i => i.Quantity > 0).WithMessage("Miqdor 0 dan katta bo'lishi kerak.");
        RuleFor(x => x.IdempotencyKey).MaximumLength(64);
    }
}
