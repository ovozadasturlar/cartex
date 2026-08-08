using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Suppliers.Commands;

public record UpdateSupplierCommand(long Id, string Name, string? Phone) : ICommand<Unit>;

public sealed class UpdateSupplierCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateSupplierCommand, Unit>
{
    public async Task<Unit> Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Supplier not found.");

        supplier.Name = request.Name;
        supplier.Phone = request.Phone;

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class UpdateSupplierCommandValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}
