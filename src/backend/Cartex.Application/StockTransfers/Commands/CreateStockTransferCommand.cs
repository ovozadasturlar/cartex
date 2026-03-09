using MediatR;
using FluentValidation;
using Cartex.Persistence;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;

namespace Cartex.Application.StockTransfers.Commands;

public record CreateStockTransferCommand(long FromWarehouseId, long ToWarehouseId, long ProductId, decimal Quantity, long UserId) : IRequest<long>;

public sealed class CreateStockTransferCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateStockTransferCommand, long>
{
    public async Task<long> Handle(CreateStockTransferCommand request, CancellationToken cancellationToken)
    {
        var transfer = new StockTransfer
        {
            FromWarehouseId = request.FromWarehouseId,
            ToWarehouseId = request.ToWarehouseId,
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            UserId = request.UserId,
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
