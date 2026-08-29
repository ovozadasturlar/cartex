using Cartex.Application.Common.Documents;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Cartex.Shared.Models.Partners;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Partners.Commands;

/// Partnership is a flag on a customer, not a separate directory entry: switching it off disables
/// the profile so the rewards already earned, and the consent already recorded, stay on file.
public sealed record SetCustomerPartnershipCommand(long CustomerId, bool IsPartner)
    : ICommand<CustomerPartnerDto?>;

public sealed class SetCustomerPartnershipCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<SetCustomerPartnershipCommand, CustomerPartnerDto?>
{
    public async Task<CustomerPartnerDto?> Handle(
        SetCustomerPartnershipCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Partners.Edit))
            throw new ForbiddenException("Hamkorlikni o'zgartirishga ruxsat yo'q.");
        var businessId = currentUser.BusinessId ?? throw new BusinessRuleException("Business not found.");

        var customer = await db.Customers
            .Include(x => x.Party).ThenInclude(x => x.PartnerProfile)
            .FirstOrDefaultAsync(x => x.Id == request.CustomerId && x.Party.BusinessId == businessId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.", "customer_not_found");

        var profile = customer.Party.PartnerProfile;
        if (profile is null)
        {
            if (!request.IsPartner) return null;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            profile = new PartnerProfile
            {
                Party = customer.Party,
                PartnerCode = await DocumentNumbers.NextAsync(db, "PRT", today, cancellationToken),
                JoinedAt = today
            };
            db.PartnerProfiles.Add(profile);
        }

        profile.IsEnabled = request.IsPartner;
        await db.SaveChangesAsync(cancellationToken);

        audit.SetOutcome(request.IsPartner ? "partner.enabled" : "partner.disabled",
            "partner_profiles", profile.Id, new
            {
                profile.PartnerCode,
                profile.PartyId,
                request.CustomerId,
                profile.IsEnabled
            }, request.IsPartner ? "Mijoz hamkor qilindi" : "Mijozning hamkorligi o'chirildi");

        return new CustomerPartnerDto(profile.Id, profile.IsEnabled,
            profile.PublicConsent.ToString(), profile.PublicVisible, profile.PublicPhoneVisible,
            profile.PublicDisplayName, profile.PublicAbout);
    }
}

public sealed class SetCustomerPartnershipCommandValidator : AbstractValidator<SetCustomerPartnershipCommand>
{
    public SetCustomerPartnershipCommandValidator() => RuleFor(x => x.CustomerId).GreaterThan(0);
}
