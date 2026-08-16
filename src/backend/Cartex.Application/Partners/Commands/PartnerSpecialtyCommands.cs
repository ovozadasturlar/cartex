using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Cartex.Shared.Models.Partners;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Partners.Commands;

public sealed record SavePartnerSpecialtyCommand(long? Id, string Name, bool IsEnabled, int SortOrder)
    : ICommand<long>;

public sealed class SavePartnerSpecialtyCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<SavePartnerSpecialtyCommand, long>
{
    public async Task<long> Handle(SavePartnerSpecialtyCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Partners.Edit))
            throw new ForbiddenException("Hamkorlarni tahrirlashga ruxsat yo'q.");
        var businessId = currentUser.BusinessId ?? throw new BusinessRuleException("Business not found.");

        var name = request.Name.Trim();
        var specialty = request.Id is { } id
            ? await db.PartnerSpecialties.FirstOrDefaultAsync(x => x.Id == id && x.BusinessId == businessId, cancellationToken)
              ?? throw new NotFoundException("Specialty not found.", "specialty_not_found")
            : new PartnerSpecialty { BusinessId = businessId };

        if (await db.PartnerSpecialties.AnyAsync(
                x => x.BusinessId == businessId && x.Name == name && x.Id != specialty.Id, cancellationToken))
            throw new BusinessRuleException("Bunday mutaxassislik allaqachon bor.", "specialty_duplicate");

        specialty.Name = name;
        specialty.IsEnabled = request.IsEnabled;
        specialty.SortOrder = request.SortOrder;
        if (request.Id is null) db.PartnerSpecialties.Add(specialty);
        await db.SaveChangesAsync(cancellationToken);

        audit.Add("partnerSpecialty", "partner_specialties", specialty.Id, new { specialty.Name, specialty.IsEnabled });
        return specialty.Id;
    }
}

public sealed class SavePartnerSpecialtyCommandValidator : AbstractValidator<SavePartnerSpecialtyCommand>
{
    public SavePartnerSpecialtyCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(60);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal static class PartnerSpecialtyLinks
{
    /// The link set is replaced, not merged: the caller sends the specialties the partner has now,
    /// and anything missing from that list is one the shop took away.
    public static async Task ApplyAsync(
        IApplicationDbContext db,
        PartnerProfile profile,
        List<long>? specialtyIds,
        long businessId,
        CancellationToken cancellationToken)
    {
        if (specialtyIds is null) return;

        var wanted = specialtyIds.Distinct().ToList();
        if (wanted.Count > 0)
        {
            var known = await db.PartnerSpecialties
                .Where(x => x.BusinessId == businessId && wanted.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            if (known.Count != wanted.Count)
                throw new NotFoundException("Specialty not found.", "specialty_not_found");
        }

        var current = await db.PartnerSpecialtyLinks
            .Where(x => x.PartnerProfileId == profile.Id)
            .ToListAsync(cancellationToken);

        foreach (var link in current.Where(x => !wanted.Contains(x.PartnerSpecialtyId)))
            db.PartnerSpecialtyLinks.Remove(link);

        foreach (var id in wanted.Where(id => current.All(x => x.PartnerSpecialtyId != id)))
            db.PartnerSpecialtyLinks.Add(new PartnerSpecialtyLink { PartnerProfile = profile, PartnerSpecialtyId = id });
    }
}
