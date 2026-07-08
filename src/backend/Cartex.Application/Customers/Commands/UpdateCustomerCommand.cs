using Cartex.Application.Common;
using Cartex.Application.Common.Messaging;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Customers.Commands;

public record UpdateCustomerCommand(long Id, string FullName, string? Phone, string? CardBarcode, decimal DiscountPct, string? Email = null, string? LastName = null, string? Address = null, decimal CreditLimit = 0, bool NotificationsOptOut = false, string? PreferredLanguage = null, long? AgentId = null) : ICommand<Unit>;

public sealed class UpdateCustomerCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateCustomerCommand, Unit>
{
    public async Task<Unit> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        customer.FullName = request.FullName;
        customer.LastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName.Trim();
        customer.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        customer.Phone = Phones.Normalize(request.Phone);
        customer.Email = request.Email;
        customer.CardBarcode = request.CardBarcode;
        customer.DiscountPct = request.DiscountPct;
        customer.CreditLimit = request.CreditLimit;
        customer.NotificationsOptOut = request.NotificationsOptOut;
        if (request.PreferredLanguage is not null)
            customer.PreferredLanguage = request.PreferredLanguage;
        if (request.AgentId is not null)
            customer.AgentId = request.AgentId == 0 ? null : request.AgentId;

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty();
        RuleFor(x => x.Phone).Must(p => Phones.IsValid(Phones.Normalize(p))).WithMessage("Telefon raqami noto'g'ri.")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.CreditLimit).GreaterThanOrEqualTo(0);
    }
}
