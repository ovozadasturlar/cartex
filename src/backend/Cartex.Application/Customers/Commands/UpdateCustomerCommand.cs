using Cartex.Application.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Customers.Commands;

public record UpdateCustomerCommand(long Id, string FullName, string? Phone, string? CardBarcode, decimal DiscountPct, string? Email = null, string? LastName = null, string? Address = null, decimal CreditLimit = 0, bool NotificationsOptOut = false, string? PreferredLanguage = null, long? AssignedUserId = null, double? Latitude = null, double? Longitude = null, long? AgentId = null) : ICommand<Unit>;

public sealed class UpdateCustomerCommandHandler(IApplicationDbContext db, ICurrentUser currentUser) : IRequestHandler<UpdateCustomerCommand, Unit>
{
    public async Task<Unit> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll) && customer.AssignedUserId != currentUser.UserId)
            throw new NotFoundException("Customer not found.");

        customer.FullName = request.FullName;
        customer.LastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName.Trim();
        customer.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        customer.Phone = Phones.Normalize(request.Phone);
        customer.Email = request.Email;
        customer.CardBarcode = request.CardBarcode;
        customer.DiscountPct = request.DiscountPct;
        customer.CreditLimit = request.CreditLimit;
        customer.NotificationsOptOut = request.NotificationsOptOut;
        var party = await db.Parties.FirstAsync(x => x.Id == customer.PartyId, cancellationToken);
        party.FullName = request.FullName.Trim();
        party.Phone = customer.Phone;
        party.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        party.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        if (request.PreferredLanguage is not null)
            customer.PreferredLanguage = request.PreferredLanguage;
        var assignedUserId = request.AssignedUserId ?? request.AgentId;
        if (assignedUserId is not null)
            customer.AssignedUserId = assignedUserId == 0 ? null : assignedUserId;
        if (request.Latitude is not null && request.Longitude is not null)
        {
            customer.Latitude = request.Latitude == 0 ? null : request.Latitude;
            customer.Longitude = request.Latitude == 0 ? null : request.Longitude;
        }

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
