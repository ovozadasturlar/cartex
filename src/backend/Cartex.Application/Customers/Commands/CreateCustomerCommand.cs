using MediatR;
using FluentValidation;
using Cartex.Persistence;
using Cartex.Domain.Entities;

namespace Cartex.Application.Customers.Commands;

public record CreateCustomerCommand(string FullName, string? Phone, string? CardBarcode, decimal DiscountPct) : ICommand<long>;

public sealed class CreateCustomerCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateCustomerCommand, long>
{
    public async Task<long> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = new Customer
        {
            FullName = request.FullName,
            Phone = request.Phone,
            CardBarcode = request.CardBarcode,
            DiscountPct = request.DiscountPct
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);

        return customer.Id;
    }
}

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty();
    }
}
