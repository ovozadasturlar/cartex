using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;

namespace Cartex.Application.StockTransfers.Commands;

public record CreateStockTransferCommand(long FromWarehouseId, long ToWarehouseId, long ProductId, decimal Quantity) : IRequest<long>;

public sealed class CreateStockTransferCommandHandler(IApplicationDbContext db, ICurrentUser currentUser) : IRequestHandler<CreateStockTransferCommand, long>
{
    public async Task<long> Handle(CreateStockTransferCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");

        var fromWarehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.FromWarehouseId, cancellationToken)
            ?? throw new NotFoundException("Source warehouse not found.");

        var transfer = new StockTransfer
        {
            BranchId = fromWarehouse.BranchId,
            FromWarehouseId = request.FromWarehouseId,
            ToWarehouseId = request.ToWarehouseId,
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            UserId = userId,
            Status = TransferStatus.Sent
        };

        db.StockTransfers.Add(transfer);
        await db.SaveChangesAsync(cancellationToken);

        return transfer.Id;
    }
}

public sealed class CreateStockTransferCommandValidator : AbstractValidator<CreateStockTransferCommand>
{
    public CreateStockTransferCommandValidator()
    {
        RuleFor(x => x.FromWarehouseId).NotEqual(x => x.ToWarehouseId);
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
