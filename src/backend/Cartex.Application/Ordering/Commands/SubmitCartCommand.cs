using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using FluentValidation;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Ordering.Commands;

public record SubmitCartItemDto(long VariantId, decimal Quantity);

public record SubmitCartCommand(long WarehouseId, long? CustomerId, List<SubmitCartItemDto> Items) : ICommand<string>;

public sealed class SubmitCartCommandHandler(IApplicationDbContext db) : IRequestHandler<SubmitCartCommand, string>
{
    public async Task<string> Handle(SubmitCartCommand request, CancellationToken cancellationToken)
    {
        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
            ?? throw new NotFoundException("Warehouse not found.");

        var cart = new Cart
        {
            BranchId = warehouse.BranchId,
            WarehouseId = request.WarehouseId,
            CustomerId = request.CustomerId,
            AggregateCode = Guid.NewGuid().ToString("N")
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
