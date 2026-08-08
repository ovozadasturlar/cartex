using FluentValidation;
using Cartex.Persistence;
using Cartex.Domain.Entities;

namespace Cartex.Application.Suppliers.Commands;

public record CreateSupplierCommand(string Name, string? Phone) : ICommand<long>;

public sealed class CreateSupplierCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateSupplierCommand, long>
{
    public async Task<long> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = new Supplier
        {
            Name = request.Name,
            Phone = request.Phone
        };

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(cancellationToken);

        return supplier.Id;
    }
}

public sealed class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}
