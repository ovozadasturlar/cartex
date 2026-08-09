using Cartex.Application.Common.Documents;
using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.TradeCases;
using Cartex.Application.Common.Participants;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.TradeCases.Commands;

public sealed record CreateTradeCaseCommand(
    long CustomerId,
    long WarehouseId,
    string Title,
    string? SiteAddress = null,
    TradeCaseWorkflow? Workflow = null,
    TradeCasePricePolicy? PricePolicy = null,
    string? Currency = null,
    DateOnly? BusinessDate = null,
    string? Note = null,
    string? IdempotencyKey = null,
    List<ParticipantInput>? Participants = null) : ICommand<TradeCaseCreatedDto>;

public sealed class CreateTradeCaseCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ISettingsService settings,
    ICurrencyService currency,
    IParticipantService participantService,
    IAuditService audit) : IRequestHandler<CreateTradeCaseCommand, TradeCaseCreatedDto>
{
    public async Task<TradeCaseCreatedDto> Handle(CreateTradeCaseCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        if (!currentUser.HasPermission(AppPermissions.TradeCases.Create))
            throw new ForbiddenException("Loyiha yaratishga ruxsat yo'q.");

        var config = await settings.GetAsync<TradeCaseSettings>(SettingKeys.TradeCases, cancellationToken)
                     ?? new TradeCaseSettings();
        if (!config.Enabled)
            throw new BusinessRuleException("Loyiha jarayoni sozlamalarda yoqilmagan.", "trade_cases_disabled");

        var warehouse = await db.Warehouses
            .Where(x => x.Id == request.WarehouseId)
            .Select(x => new { x.Id, x.BranchId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Warehouse not found.", "warehouse_not_found");
        if (!currentUser.CanAccessAllBranches && !currentUser.BranchIds.Contains(warehouse.BranchId))
            throw new NotFoundException("Warehouse not found.", "warehouse_not_found");

        var customerExists = await db.Customers.AnyAsync(x => x.Id == request.CustomerId, cancellationToken);
        if (!customerExists)
            throw new NotFoundException("Customer not found.", "customer_not_found");

        var idempotencyKey = NormalizeOptional(request.IdempotencyKey);
        if (idempotencyKey is not null)
        {
            var existing = await db.TradeCases
                .Where(x => x.BranchId == warehouse.BranchId && x.IdempotencyKey == idempotencyKey)
                .Select(x => new TradeCaseCreatedDto(x.Id, x.CaseNumber, x.Version))
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null) return existing;
        }

        if (config.RequireSiteAddress && string.IsNullOrWhiteSpace(request.SiteAddress))
            throw new BusinessRuleException("Manzil kiritilishi shart.", "site_address_required");
        if (!config.AllowWorkflowOverride && request.Workflow.HasValue
            && request.Workflow != config.DefaultWorkflow)
            throw new BusinessRuleException("Jarayon turini o'zgartirishga ruxsat yo'q.", "workflow_override_disabled");
        if (!config.AllowPricePolicyOverride && request.PricePolicy.HasValue
            && request.PricePolicy != config.DefaultPricePolicy)
            throw new BusinessRuleException("Narx siyosatini o'zgartirishga ruxsat yo'q.", "price_policy_override_disabled");

        var baseCode = (await currency.BaseAsync(cancellationToken)).ToUpperInvariant();
        var code = string.IsNullOrWhiteSpace(request.Currency)
            ? baseCode
            : request.Currency.Trim().ToUpperInvariant();
        await currency.EnsureSalesAllowedAsync(code, cancellationToken);
        var resolvedParticipants = await participantService.ResolveAsync(
            request.Participants, ParticipantContext.TradeCase, request.CustomerId, cancellationToken);
        var businessDate = request.BusinessDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var tradeCase = new TradeCase
        {
            BranchId = warehouse.BranchId,
            WarehouseId = warehouse.Id,
            CustomerId = request.CustomerId,
            CaseNumber = await DocumentNumbers.NextAsync(db, "CASE", businessDate, cancellationToken),
            BusinessDate = businessDate,
            Title = request.Title.Trim(),
            SiteAddress = NormalizeOptional(request.SiteAddress),
            Currency = code,
            Workflow = request.Workflow ?? config.DefaultWorkflow,
            PricePolicy = request.PricePolicy ?? config.DefaultPricePolicy,
            Note = NormalizeOptional(request.Note),
            IdempotencyKey = idempotencyKey,
            CreatedBy = userId
        };
        foreach (var participant in resolvedParticipants)
            tradeCase.Participants.Add(new TradeCaseParticipant
            {
                RoleDefinitionId = participant.RoleDefinitionId,
                PartyId = participant.PartyId,
                PartyNameSnapshot = participant.PartyName,
                PartyPhoneSnapshot = participant.PartyPhone,
                RoleLabelSnapshot = participant.RoleLabel
            });
        db.TradeCases.Add(tradeCase);
        await db.SaveChangesAsync(cancellationToken);

        audit.SetOutcome("case.opened", "trade_cases", tradeCase.Id, new
        {
            tradeCase.CaseNumber,
            tradeCase.CustomerId,
            tradeCase.WarehouseId,
            tradeCase.Title,
            tradeCase.Workflow,
            tradeCase.PricePolicy,
            tradeCase.Currency,
            participants = tradeCase.Participants.Select(x => new { x.RoleDefinitionId, x.PartyId, x.RoleLabelSnapshot })
        }, $"{config.SingularLabel} ochildi", tradeCase.BranchId);
        return new TradeCaseCreatedDto(tradeCase.Id, tradeCase.CaseNumber, tradeCase.Version);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class CreateTradeCaseCommandValidator : AbstractValidator<CreateTradeCaseCommand>
{
    public CreateTradeCaseCommandValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SiteAddress).MaximumLength(300);
        RuleFor(x => x.Currency).MaximumLength(3);
        RuleFor(x => x.Note).MaximumLength(2000);
        RuleFor(x => x.IdempotencyKey).MaximumLength(128);
    }
}
