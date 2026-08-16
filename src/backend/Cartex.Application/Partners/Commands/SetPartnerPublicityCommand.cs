using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Partners.Commands;

public sealed record SetPartnerPublicityCommand(
    long PartnerId,
    PublicConsentState Consent,
    bool PublicVisible,
    bool PublicPhoneVisible,
    string? PublicDisplayName,
    string? PublicAbout) : ICommand<Unit>;

public sealed class SetPartnerPublicityCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<SetPartnerPublicityCommand, Unit>
{
    public async Task<Unit> Handle(SetPartnerPublicityCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Partners.Publish))
            throw new ForbiddenException("Hamkorni ommaga chiqarishga ruxsat yo'q.");
        var businessId = currentUser.BusinessId ?? throw new BusinessRuleException("Business not found.");

        var profile = await db.PartnerProfiles
            .Include(x => x.Party)
            .FirstOrDefaultAsync(x => x.Id == request.PartnerId && x.Party.BusinessId == businessId, cancellationToken)
            ?? throw new NotFoundException("Partner not found.", "partner_not_found");

        // Nothing goes public without a recorded yes. Anything else — never asked, refused,
        // taken back — means the page must not carry this person, whatever the toggle says.
        if (request.Consent != PublicConsentState.Granted && (request.PublicVisible || request.PublicPhoneVisible))
            throw new BusinessRuleException(
                "Rozilik berilmagan hamkorni ommaga chiqarib bo'lmaydi.", "public_consent_required");

        if (profile.PublicConsent != request.Consent)
        {
            profile.PublicConsent = request.Consent;
            profile.PublicConsentAt = DateTime.UtcNow;
            profile.PublicConsentByUserId = currentUser.UserId;
            // Recorded through the back office. Self-service will set SelfService instead (HAMKOR-12).
            profile.PublicConsentSource = PublicConsentSource.Staff;
        }

        profile.PublicVisible = request.PublicVisible;
        profile.PublicPhoneVisible = request.PublicPhoneVisible;
        profile.PublicDisplayName = Normalize(request.PublicDisplayName);
        profile.PublicAbout = Normalize(request.PublicAbout);
        await db.SaveChangesAsync(cancellationToken);

        audit.SetOutcome("partner.publicity", "partner_profiles", profile.Id, new
        {
            profile.PartnerCode,
            profile.PublicConsent,
            profile.PublicVisible,
            profile.PublicPhoneVisible,
            profile.PublicConsentAt,
            profile.PublicConsentByUserId,
            profile.PublicConsentSource
        }, "Hamkorning ommaviy ko'rinishi o'zgartirildi");

        return Unit.Value;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class SetPartnerPublicityCommandValidator : AbstractValidator<SetPartnerPublicityCommand>
{
    public SetPartnerPublicityCommandValidator()
    {
        RuleFor(x => x.PartnerId).GreaterThan(0);
        RuleFor(x => x.PublicDisplayName).MaximumLength(120);
        RuleFor(x => x.PublicAbout).MaximumLength(600);
    }
}
