using FluentValidation;
using Cartex.Persistence;
using Cartex.Domain.Entities;

namespace Cartex.Application.Warehouses.Commands;

public record CreateWarehouseCommand(long BranchId, string Name, bool IsOnline = false) : ICommand<long>;

public sealed class CreateWarehouseCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateWarehouseCommand, long>
{
    public async Task<long> Handle(CreateWarehouseCommand request, CancellationToken cancellationToken)
    {
        var warehouse = new Warehouse
        {
            BranchId = request.BranchId,
            Name = request.Name,
            IsOnline = request.IsOnline
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
