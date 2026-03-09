using MediatR;
using FluentValidation;
using Cartex.Persistence;
using Cartex.Domain.Entities;

namespace Cartex.Application.Warehouses.Commands;

public record CreateWarehouseCommand(long ShopId, string Name) : IRequest<long>;

public sealed class CreateWarehouseCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateWarehouseCommand, long>
{
    public async Task<long> Handle(CreateWarehouseCommand request, CancellationToken cancellationToken)
    {
        var warehouse = new Warehouse
        {
            ShopId = request.ShopId,
            Name = request.Name
        };

        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync(cancellationToken);

        return warehouse.Id;
    }
}

public sealed class CreateWarehouseCommandValidator : AbstractValidator<CreateWarehouseCommand>
{
    public CreateWarehouseCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}
