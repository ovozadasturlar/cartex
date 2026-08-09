using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Participants;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.TradeCases.Commands;

public sealed record UpdateTradeCaseCommand(
    long Id,
    string Title,
    string? SiteAddress = null,
    string? Note = null,
    List<ParticipantInput>? Participants = null,
    int? ExpectedVersion = null) : ICommand<Unit>;

public sealed class UpdateTradeCaseCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IParticipantService participantService,
    IAuditService audit) : IRequestHandler<UpdateTradeCaseCommand, Unit>
{
    public async Task<Unit> Handle(UpdateTradeCaseCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.TradeCases.Edit))
            throw new ForbiddenException("Loyihani tahrirlashga ruxsat yo'q.");
        var tradeCase = await db.TradeCases
            .FromSqlInterpolated($"SELECT * FROM trade_cases WHERE id = {request.Id} FOR UPDATE")
            .Include(x => x.Participants)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Trade case not found.", "trade_case_not_found");
        if (tradeCase.Status is TradeCaseStatus.Settled or TradeCaseStatus.Cancelled)
            throw new ConflictException("Yakunlangan loyiha tahrirlanmaydi.", "trade_case_not_editable");
        EnsureVersion(tradeCase, request.ExpectedVersion);
        var participants = await participantService.ResolveAsync(
            request.Participants, ParticipantContext.TradeCase, tradeCase.CustomerId, cancellationToken);

        tradeCase.Title = request.Title.Trim();
        tradeCase.SiteAddress = Normalize(request.SiteAddress);
        tradeCase.Note = Normalize(request.Note);
        tradeCase.Participants.Clear();
        foreach (var row in participants)
            tradeCase.Participants.Add(new TradeCaseParticipant
            {
                RoleDefinitionId = row.RoleDefinitionId,
                PartyId = row.PartyId,
                PartyNameSnapshot = row.PartyName,
                PartyPhoneSnapshot = row.PartyPhone,
                RoleLabelSnapshot = row.RoleLabel
            });
        tradeCase.Version++;
        await db.SaveChangesAsync(cancellationToken);
        audit.SetOutcome("case.updated", "trade_cases", tradeCase.Id, new
        {
            tradeCase.CaseNumber,
            tradeCase.Title,
            tradeCase.SiteAddress,
            tradeCase.Note,
            tradeCase.Version,
            participants = tradeCase.Participants.Select(x => new { x.RoleDefinitionId, x.PartyId })
        }, "Loyiha yangilandi", tradeCase.BranchId);
        return Unit.Value;
    }

    private static void EnsureVersion(TradeCase tradeCase, int? expected)
    {
        if (expected.HasValue && expected != tradeCase.Version)
            throw new ConflictException("Loyiha boshqa qurilmada o'zgartirilgan.", "trade_case_version_conflict");
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class UpdateTradeCaseCommandValidator : AbstractValidator<UpdateTradeCaseCommand>
{
    public UpdateTradeCaseCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SiteAddress).MaximumLength(300);
        RuleFor(x => x.Note).MaximumLength(2000);
    }
}

public sealed record ChangeTradeCaseStatusCommand(
    long Id,
    TradeCaseStatus Status,
    string? Reason = null,
    int? ExpectedVersion = null) : ICommand<Unit>;

public sealed class ChangeTradeCaseStatusCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<ChangeTradeCaseStatusCommand, Unit>
{
    public async Task<Unit> Handle(ChangeTradeCaseStatusCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.TradeCases.Close))
            throw new ForbiddenException("Loyihani yopishga ruxsat yo'q.");
        if (request.Status is not (TradeCaseStatus.Settled or TradeCaseStatus.Cancelled))
            throw new BusinessRuleException("Loyiha yakuniy holati noto'g'ri.", "invalid_trade_case_status");
        var tradeCase = await db.TradeCases
            .FromSqlInterpolated($"SELECT * FROM trade_cases WHERE id = {request.Id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Trade case not found.", "trade_case_not_found");
        if (request.ExpectedVersion.HasValue && request.ExpectedVersion != tradeCase.Version)
            throw new ConflictException("Loyiha boshqa qurilmada o'zgartirilgan.", "trade_case_version_conflict");
        if (tradeCase.Status is TradeCaseStatus.Settled or TradeCaseStatus.Cancelled)
            throw new ConflictException("Loyiha allaqachon yakunlangan.", "trade_case_already_closed");

        var quantities = await db.GoodsIssueLines.Where(x => x.Document.TradeCaseId == tradeCase.Id
                && x.Document.Status == BusinessDocumentStatus.Posted)
            .Select(x => new { Outstanding = x.Quantity - x.ReturnedQuantity - x.SettledQuantity })
            .ToListAsync(cancellationToken);
        if (quantities.Any(x => x.Outstanding > 0))
            throw new BusinessRuleException("Avval mijozdagi mahsulotlarni qaytaring yoki hisob-kitob qiling.",
                "trade_case_has_custody");
        if (request.Status == TradeCaseStatus.Cancelled && quantities.Count > 0)
            throw new BusinessRuleException("Harakat boshlangan loyiha bekor qilinmaydi; uni yoping.",
                "trade_case_has_documents");

        var from = tradeCase.Status;
        tradeCase.Status = request.Status;
        tradeCase.Version++;
        if (!string.IsNullOrWhiteSpace(request.Reason))
            tradeCase.Note = string.IsNullOrWhiteSpace(tradeCase.Note)
                ? request.Reason.Trim()
                : $"{tradeCase.Note}\n{request.Reason.Trim()}";
        await db.SaveChangesAsync(cancellationToken);
        audit.SetOutcome(request.Status == TradeCaseStatus.Cancelled ? "case.cancelled" : "case.closed",
            "trade_cases", tradeCase.Id, new
            {
                tradeCase.CaseNumber,
                from,
                to = request.Status,
                reason = request.Reason,
                tradeCase.Version
            }, request.Status == TradeCaseStatus.Cancelled ? "Loyiha bekor qilindi" : "Loyiha yopildi",
            tradeCase.BranchId);
        return Unit.Value;
    }
}
