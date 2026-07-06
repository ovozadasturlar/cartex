using Cartex.Application.Common.Messaging;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Stocks.Commands;

public record AdjustStockCommand(long WarehouseId, long VariantId, decimal CountedQuantity, string? Reason) : ICommand<Unit>;

public sealed class AdjustStockCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, IAuditService audit)
    : IRequestHandler<AdjustStockCommand, Unit>
{
    public async Task<Unit> Handle(AdjustStockCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");

        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
            ?? throw new NotFoundException("Warehouse not found.");

        var stocks = await db.Stocks
            .Where(s => s.WarehouseId == request.WarehouseId && s.VariantId == request.VariantId)
            .OrderByDescending(s => s.Quantity)
            .ToListAsync(cancellationToken);

        var systemQuantity = stocks.Sum(s => s.Quantity);
        var difference = request.CountedQuantity - systemQuantity;

        var target = stocks.FirstOrDefault();
        var stockId = target?.Id;

        if (difference != 0)
        {
            if (target is null)
            {
                target = new Stock
                {
                    BranchId = warehouse.BranchId,
                    WarehouseId = request.WarehouseId,
                    VariantId = request.VariantId
                };
                db.Stocks.Add(target);
            }
            target.Quantity += difference;
        }

        db.StockAdjustments.Add(new StockAdjustment
        {
            BranchId = warehouse.BranchId,
            WarehouseId = request.WarehouseId,
            VariantId = request.VariantId,
            StockId = stockId,
            SystemQuantity = systemQuantity,
            CountedQuantity = request.CountedQuantity,
            Difference = difference,
            Reason = request.Reason,
            UserId = userId
        });

        audit.Add("adjust", "stocks", stockId, new { request.WarehouseId, request.VariantId, systemQuantity, request.CountedQuantity, difference, request.Reason });

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockCommandValidator()
    {
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.VariantId).GreaterThan(0);
        RuleFor(x => x.CountedQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
