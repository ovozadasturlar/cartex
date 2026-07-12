using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using FluentValidation;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Ordering.Commands;

public record SubmitCartItemDto(long VariantId, decimal Quantity);

public record SubmitCartCommand(long WarehouseId, long? CustomerId, List<SubmitCartItemDto> Items, string? IdempotencyKey = null, string? Note = null) : ICommand<string>;

public sealed class SubmitCartCommandHandler(IApplicationDbContext db) : IRequestHandler<SubmitCartCommand, string>
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
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
        };

        foreach (var item in request.Items)
            cart.Items.Add(new CartItem { VariantId = item.VariantId, Quantity = item.Quantity });

        db.Carts.Add(cart);
        await db.SaveChangesAsync(cancellationToken);

        return cart.AggregateCode;
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
