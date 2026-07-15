using Cartex.Application.Common.Messaging;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Stocks.Commands;

public record AddOpeningStockCommand(long WarehouseId, long VariantId, decimal Quantity, decimal PurchasePrice, DateOnly? ExpiredAt) : ICommand<Unit>;

public sealed class AddOpeningStockCommandHandler(IApplicationDbContext db, IAuditService audit)
    : IRequestHandler<AddOpeningStockCommand, Unit>
{
    public async Task<Unit> Handle(AddOpeningStockCommand request, CancellationToken cancellationToken)
    {
        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
            ?? throw new NotFoundException("Warehouse not found.");

        if (!await db.ProductVariants.AnyAsync(v => v.Id == request.VariantId, cancellationToken))
            throw new NotFoundException("Variant not found.");

        db.Stocks.Add(new Stock
        {
            BranchId = warehouse.BranchId,
            WarehouseId = request.WarehouseId,
            VariantId = request.VariantId,
            Quantity = request.Quantity,
            PurchasePrice = request.PurchasePrice,
            ExpiredAt = request.ExpiredAt
        });

        audit.Add("opening", "stocks", null, new { request.WarehouseId, request.VariantId, request.Quantity, request.PurchasePrice, request.ExpiredAt });

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class AddOpeningStockCommandValidator : AbstractValidator<AddOpeningStockCommand>
{
    public AddOpeningStockCommandValidator()
    {
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.VariantId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.PurchasePrice).GreaterThanOrEqualTo(0);
    }
}
