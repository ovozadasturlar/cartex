using Cartex.Application.Common.Messaging;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Warehouses.Commands;

public record UpdateWarehouseCommand(long Id, string Name, bool IsOnline = false) : ICommand<Unit>;

public sealed class UpdateWarehouseCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateWarehouseCommand, Unit>
{
    public async Task<Unit> Handle(UpdateWarehouseCommand request, CancellationToken cancellationToken)
    {
        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Warehouse not found.");

        warehouse.Name = request.Name;
        warehouse.IsOnline = request.IsOnline;

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class UpdateWarehouseCommandValidator : AbstractValidator<UpdateWarehouseCommand>
{
    public UpdateWarehouseCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}
