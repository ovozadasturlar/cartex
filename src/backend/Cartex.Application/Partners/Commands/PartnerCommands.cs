using Cartex.Application.Common;
using Cartex.Application.Common.Documents;
using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Partners;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Partners.Commands;

public sealed record CreatePartnerCommand(
    string FullName,
    string? Phone = null,
    string? Email = null,
    string? Address = null,
    long? CustomerId = null,
    string? Note = null) : ICommand<long>;

public sealed class CreatePartnerCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<CreatePartnerCommand, long>
{
    public async Task<long> Handle(CreatePartnerCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Partners.Edit))
            throw new ForbiddenException("Hamkor yaratishga ruxsat yo'q.");
        var businessId = currentUser.BusinessId
            ?? await db.Businesses.Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException("Business not found.");
        var phone = Phones.Normalize(request.Phone);

        Party? party = null;
        if (request.CustomerId is { } customerId)
        {
            party = await db.Customers.Where(x => x.Id == customerId)
                .Select(x => x.Party).FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Customer not found.", "customer_not_found");
        }
        else if (phone is not null)
        {
            party = await db.Parties.FirstOrDefaultAsync(x =>
                x.BusinessId == businessId && x.Phone == phone, cancellationToken);
        }

        party ??= new Party
        {
            BusinessId = businessId,
            FullName = request.FullName.Trim(),
            Phone = phone,
            Email = NormalizeOptional(request.Email),
            Address = NormalizeOptional(request.Address)
        };
        if (party.Id == 0) db.Parties.Add(party);
        var existing = party.Id == 0 ? null : await db.PartnerProfiles
            .FirstOrDefaultAsync(x => x.PartyId == party.Id, cancellationToken);
        if (existing is not null)
        {
            if (!existing.IsEnabled) existing.IsEnabled = true;
            await db.SaveChangesAsync(cancellationToken);
            return existing.Id;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var profile = new PartnerProfile
        {
            Party = party,
            PartnerCode = await DocumentNumbers.NextAsync(db, "PRT", today, cancellationToken),
            JoinedAt = today,
            Note = NormalizeOptional(request.Note)
        };
        db.PartnerProfiles.Add(profile);
        await db.SaveChangesAsync(cancellationToken);
        audit.SetOutcome("partner.created", "partner_profiles", profile.Id, new
        {
            profile.PartnerCode,
            profile.PartyId,
            party.FullName,
            party.Phone,
            request.CustomerId
        }, "Hamkor yaratildi");
        return profile.Id;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class CreatePartnerCommandValidator : AbstractValidator<CreatePartnerCommand>
{
    public CreatePartnerCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).Must(x => string.IsNullOrWhiteSpace(x) || Phones.IsValid(Phones.Normalize(x)))
            .WithMessage("Telefon raqami noto'g'ri.");
        RuleFor(x => x.Email).MaximumLength(120);
        RuleFor(x => x.Address).MaximumLength(300);
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}

public sealed record UpdatePartnerCommand(
    long Id,
    string FullName,
    string? Phone = null,
    string? Email = null,
    string? Address = null,
    bool IsEnabled = true,
    string? Note = null) : ICommand<Cartex.Application.Common.Messaging.Unit>;

public sealed class UpdatePartnerCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<UpdatePartnerCommand, Cartex.Application.Common.Messaging.Unit>
{
    public async Task<Cartex.Application.Common.Messaging.Unit> Handle(UpdatePartnerCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Partners.Edit))
            throw new ForbiddenException("Hamkorni o'zgartirishga ruxsat yo'q.");
        var businessId = currentUser.BusinessId ?? throw new BusinessRuleException("Business not found.");
        var profile = await db.PartnerProfiles.Include(x => x.Party)
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.Party.BusinessId == businessId, cancellationToken)
            ?? throw new NotFoundException("Partner not found.", "partner_not_found");
        var phone = Phones.Normalize(request.Phone);
        if (phone is not null && await db.Parties.AnyAsync(x =>
                x.BusinessId == businessId && x.Phone == phone && x.Id != profile.PartyId, cancellationToken))
            throw new ConflictException("Bu telefon raqamli shaxs mavjud.", "party_phone_exists");

        profile.Party.FullName = request.FullName.Trim();
        profile.Party.Phone = phone;
        profile.Party.Email = NormalizeOptional(request.Email);
        profile.Party.Address = NormalizeOptional(request.Address);
        profile.IsEnabled = request.IsEnabled;
        profile.Note = NormalizeOptional(request.Note);
        await db.SaveChangesAsync(cancellationToken);
        audit.SetOutcome("partner.updated", "partner_profiles", profile.Id, new
        {
            profile.PartnerCode,
            profile.Party.FullName,
            profile.Party.Phone,
            profile.IsEnabled,
            profile.Note
        }, "Hamkor ma'lumoti yangilandi");
        return Cartex.Application.Common.Messaging.Unit.Value;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class UpdatePartnerCommandValidator : AbstractValidator<UpdatePartnerCommand>
{
    public UpdatePartnerCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).Must(x => string.IsNullOrWhiteSpace(x) || Phones.IsValid(Phones.Normalize(x)))
            .WithMessage("Telefon raqami noto'g'ri.");
        RuleFor(x => x.Email).MaximumLength(120);
        RuleFor(x => x.Address).MaximumLength(300);
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}

public sealed record SaveParticipantRoleCommand(
    long? Id,
    string Key,
    string SingularLabel,
    string PluralLabel,
    bool IsEnabled = true,
    bool IsRequired = false,
    bool CanEqualBuyer = true,
    int MaxCount = 1,
    bool AppliesToCart = true,
    bool AppliesToSale = true,
    int SortOrder = 0) : ICommand<long>;

public sealed class SaveParticipantRoleCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<SaveParticipantRoleCommand, long>
{
    public async Task<long> Handle(SaveParticipantRoleCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Partners.ConfigureRoles))
            throw new ForbiddenException("Hamkor rollarini sozlashga ruxsat yo'q.");
        var businessId = currentUser.BusinessId ?? throw new BusinessRuleException("Business not found.");
        var key = request.Key.Trim().ToLowerInvariant().Replace(' ', '_');
        var entity = request.Id is { } id
            ? await db.ParticipantRoleDefinitions.FirstOrDefaultAsync(x => x.Id == id && x.BusinessId == businessId,
                cancellationToken) ?? throw new NotFoundException("Participant role not found.")
            : new ParticipantRoleDefinition { BusinessId = businessId, Key = key };
        var isNew = entity.Id == 0;
        if (entity.Id > 0 && entity.Key != key)
            throw new BusinessRuleException("Mavjud rolning texnik kalitini o'zgartirib bo'lmaydi.", "participant_role_key_immutable");
        if (entity.Id == 0 && await db.ParticipantRoleDefinitions.AnyAsync(x =>
                x.BusinessId == businessId && x.Key == key, cancellationToken))
            throw new ConflictException("Bu kalitli rol mavjud.", "participant_role_exists");

        entity.SingularLabel = request.SingularLabel.Trim();
        entity.PluralLabel = request.PluralLabel.Trim();
        entity.IsEnabled = request.IsEnabled;
        entity.IsRequired = request.IsRequired;
        entity.CanEqualBuyer = request.CanEqualBuyer;
        entity.MaxCount = request.MaxCount;
        entity.AppliesToCart = request.AppliesToCart;
        entity.AppliesToSale = request.AppliesToSale;
        entity.SortOrder = request.SortOrder;
        if (entity.Id == 0) db.ParticipantRoleDefinitions.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        audit.SetOutcome(isNew ? "participant_role.created" : "participant_role.updated",
            "participant_role_definitions", entity.Id, new
            {
                entity.Key, entity.SingularLabel, entity.PluralLabel, entity.IsEnabled,
                entity.IsRequired, entity.CanEqualBuyer, entity.MaxCount,
                entity.AppliesToCart, entity.AppliesToSale
            }, "Hamkor roli saqlandi");
        return entity.Id;
    }
}

public sealed class SaveParticipantRoleCommandValidator : AbstractValidator<SaveParticipantRoleCommand>
{
    public SaveParticipantRoleCommandValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(40).Matches("^[a-zA-Z0-9_ -]+$");
        RuleFor(x => x.SingularLabel).NotEmpty().MaximumLength(40);
        RuleFor(x => x.PluralLabel).NotEmpty().MaximumLength(60);
        RuleFor(x => x.MaxCount).InclusiveBetween(1, 10);
    }
}

public sealed record PartnerRewardRuleInput(
    CashbackScope Scope,
    long TargetId,
    bool IsExcluded = false,
    decimal? ValueOverride = null,
    int Priority = 0);

public sealed record SavePartnerProgramCommand(
    long? Id,
    long RoleDefinitionId,
    string Name,
    bool IsEnabled,
    PartnerRewardMode Mode,
    PartnerRewardBasis Basis,
    PartnerRewardTrigger Trigger,
    decimal Value,
    long? BranchId = null,
    decimal? CapPerSale = null,
    int HoldDays = 0,
    List<PartnerRewardRuleInput>? Rules = null) : ICommand<long>;

public sealed class SavePartnerProgramCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<SavePartnerProgramCommand, long>
{
    public async Task<long> Handle(SavePartnerProgramCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.PartnerRewards.Configure))
            throw new ForbiddenException("Hamkor mukofotini sozlashga ruxsat yo'q.");
        var businessId = currentUser.BusinessId ?? throw new BusinessRuleException("Business not found.");
        if (!await db.ParticipantRoleDefinitions.AnyAsync(x =>
                x.Id == request.RoleDefinitionId && x.BusinessId == businessId, cancellationToken))
            throw new NotFoundException("Participant role not found.");
        if (request.BranchId.HasValue && !await db.Branches.AnyAsync(x =>
                x.Id == request.BranchId && x.BusinessId == businessId, cancellationToken))
            throw new NotFoundException("Branch not found.");

        var entity = request.Id is { } id
            ? await db.PartnerPrograms.Include(x => x.Rules).FirstOrDefaultAsync(x =>
                x.Id == id && x.BusinessId == businessId, cancellationToken)
              ?? throw new NotFoundException("Partner program not found.")
            : new PartnerProgram { BusinessId = businessId };
        entity.RoleDefinitionId = request.RoleDefinitionId;
        entity.Name = request.Name.Trim();
        entity.IsEnabled = request.IsEnabled;
        entity.Mode = request.Mode;
        entity.Basis = request.Basis;
        entity.Trigger = request.Trigger;
        entity.Value = request.Value;
        entity.BranchId = request.BranchId;
        entity.CapPerSale = request.CapPerSale;
        entity.HoldDays = request.HoldDays;
        if (entity.Id == 0) db.PartnerPrograms.Add(entity);
        else db.PartnerRewardRules.RemoveRange(entity.Rules);
        entity.Rules = (request.Rules ?? []).Select(x => new PartnerRewardRule
        {
            Scope = x.Scope,
            TargetId = x.TargetId,
            IsExcluded = x.IsExcluded,
            ValueOverride = x.ValueOverride,
            Priority = x.Priority
        }).ToList();
        await db.SaveChangesAsync(cancellationToken);
        audit.SetOutcome("partner_program.saved", "partner_programs", entity.Id, new
        {
            entity.RoleDefinitionId, entity.Name, entity.IsEnabled, entity.Mode,
            entity.Basis, entity.Trigger, entity.Value, entity.BranchId,
            entity.CapPerSale, entity.HoldDays, rules = entity.Rules.Count
        }, "Hamkor mukofot dasturi saqlandi", request.BranchId);
        return entity.Id;
    }
}

public sealed class SavePartnerProgramCommandValidator : AbstractValidator<SavePartnerProgramCommand>
{
    public SavePartnerProgramCommandValidator()
    {
        RuleFor(x => x.RoleDefinitionId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Value).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CapPerSale).GreaterThan(0).When(x => x.CapPerSale.HasValue);
        RuleFor(x => x.HoldDays).InclusiveBetween(0, 3650);
        RuleFor(x => x.Rules).Must(x => x is null || x.Count <= 1000);
        RuleForEach(x => x.Rules!).ChildRules(x =>
        {
            x.RuleFor(y => y.TargetId).GreaterThan(0);
            x.RuleFor(y => y.ValueOverride).GreaterThanOrEqualTo(0).When(y => y.ValueOverride.HasValue);
        }).When(x => x.Rules is not null);
    }
}

public sealed record AdjustPartnerRewardCommand(
    long PartnerId,
    long ProgramId,
    decimal Amount,
    long? BranchId = null,
    string? Note = null,
    Guid? EventId = null) : ICommand<PartnerRewardAdjustmentResult>;

public sealed class AdjustPartnerRewardCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<AdjustPartnerRewardCommand, PartnerRewardAdjustmentResult>
{
    public async Task<PartnerRewardAdjustmentResult> Handle(
        AdjustPartnerRewardCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.PartnerRewards.Adjust))
            throw new ForbiddenException("Hamkor mukofotini tuzatishga ruxsat yo'q.");
        var businessId = currentUser.BusinessId ?? throw new BusinessRuleException("Business not found.");
        var branchId = request.BranchId ?? currentUser.DefaultBranchId
            ?? throw new BusinessRuleException("Filial tanlanishi kerak.", "branch_required");
        if (!currentUser.CanAccessAllBranches && !currentUser.BranchIds.Contains(branchId))
            throw new ForbiddenException("Bu filialga ruxsat yo'q.");
        var eventId = request.EventId ?? Guid.NewGuid();
        var existing = await db.PartnerRewardEntries.Where(x => x.EventId == eventId)
            .Select(x => new PartnerRewardAdjustmentResult(x.Id, x.EventId, x.Amount))
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null) return existing;

        var partner = await db.PartnerProfiles
            .FromSqlInterpolated($"SELECT * FROM partner_profiles WHERE id = {request.PartnerId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Partner not found.", "partner_not_found");
        if (!await db.Parties.AnyAsync(x => x.Id == partner.PartyId && x.BusinessId == businessId,
                cancellationToken))
            throw new NotFoundException("Partner not found.", "partner_not_found");
        var program = await db.PartnerPrograms.FirstOrDefaultAsync(x =>
            x.Id == request.ProgramId && x.BusinessId == businessId, cancellationToken)
            ?? throw new NotFoundException("Partner program not found.", "partner_program_not_found");

        var entry = new PartnerRewardEntry
        {
            EventId = eventId,
            BranchId = branchId,
            PartnerProfileId = partner.Id,
            PartnerProgramId = program.Id,
            Mode = program.Mode,
            State = request.Amount > 0 ? PartnerRewardState.Earned : PartnerRewardState.Reversed,
            Amount = request.Amount,
            AvailableAt = DateTime.UtcNow,
            DetailsJson = System.Text.Json.JsonSerializer.Serialize(new
                { type = "manual_adjustment", note = NormalizeOptional(request.Note) })
        };
        db.PartnerRewardEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        audit.SetOutcome("partner.reward_adjusted", "partner_reward_entries", entry.Id, new
        {
            entry.EventId,
            entry.PartnerProfileId,
            entry.PartnerProgramId,
            entry.Mode,
            entry.Amount,
            note = request.Note
        }, "Hamkor mukofoti tuzatildi", branchId);
        return new PartnerRewardAdjustmentResult(entry.Id, entry.EventId, entry.Amount);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class AdjustPartnerRewardCommandValidator : AbstractValidator<AdjustPartnerRewardCommand>
{
    public AdjustPartnerRewardCommandValidator()
    {
        RuleFor(x => x.PartnerId).GreaterThan(0);
        RuleFor(x => x.ProgramId).GreaterThan(0);
        RuleFor(x => x.Amount).NotEqual(0);
        RuleFor(x => x.Note).NotEmpty().MaximumLength(1000);
    }
}
