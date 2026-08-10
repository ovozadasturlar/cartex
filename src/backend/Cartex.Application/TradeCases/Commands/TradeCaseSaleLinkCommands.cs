using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.TradeCases.Commands;

public sealed record LinkSaleToTradeCaseCommand(long CaseId, long SaleId, int? ExpectedVersion = null) : ICommand<Unit>;

public sealed class LinkSaleToTradeCaseCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<LinkSaleToTradeCaseCommand, Unit>
{
    public async Task<Unit> Handle(LinkSaleToTradeCaseCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.TradeCases.Edit))
            throw new ForbiddenException("Loyihani tahrirlashga ruxsat yo'q.");
        var tradeCase = await db.TradeCases
            .FromSqlInterpolated($"SELECT * FROM trade_cases WHERE id = {request.CaseId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Trade case not found.", "trade_case_not_found");
        if (tradeCase.Status is TradeCaseStatus.Settled or TradeCaseStatus.Cancelled)
            throw new ConflictException("Yakunlangan loyihaga savdo biriktirib bo'lmaydi.", "trade_case_not_editable");
        if (request.ExpectedVersion.HasValue && request.ExpectedVersion != tradeCase.Version)
            throw new ConflictException("Loyiha boshqa qurilmada o'zgartirilgan.", "trade_case_version_conflict");

        var sale = await db.Sales
            .FromSqlInterpolated($"SELECT * FROM sales WHERE id = {request.SaleId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sale not found.", "sale_not_found");
        if (sale.TradeCaseId == tradeCase.Id)
            return Unit.Value;
        if (sale.TradeCaseId is not null)
            throw new ConflictException("Savdo boshqa loyihaga biriktirilgan.", "sale_already_linked");
        if (sale.Status != SaleStatus.Completed)
            throw new BusinessRuleException("Faqat yakunlangan savdo loyihaga biriktiriladi.", "sale_not_completed");
        if (sale.BranchId != tradeCase.BranchId)
            throw new ConflictException("Savdo va loyiha turli filiallarda.", "sale_branch_mismatch");
        if (sale.CustomerId != tradeCase.CustomerId)
            throw new ConflictException("Savdo boshqa mijozga tegishli.", "sale_customer_mismatch");

        sale.TradeCaseId = tradeCase.Id;
        tradeCase.Version++;
        await db.SaveChangesAsync(cancellationToken);
        audit.SetOutcome("case.sale_linked", "trade_cases", tradeCase.Id, new
        {
            tradeCase.CaseNumber,
            saleId = sale.Id,
            sale.ReceiptToken,
            sale.TotalAmount,
            tradeCase.Version
        }, "Savdo loyihaga biriktirildi", tradeCase.BranchId);
        return Unit.Value;
    }
}

public sealed class LinkSaleToTradeCaseCommandValidator : AbstractValidator<LinkSaleToTradeCaseCommand>
{
    public LinkSaleToTradeCaseCommandValidator()
    {
        RuleFor(x => x.CaseId).GreaterThan(0);
        RuleFor(x => x.SaleId).GreaterThan(0);
    }
}

public sealed record UnlinkSaleFromTradeCaseCommand(long CaseId, long SaleId, int? ExpectedVersion = null) : ICommand<Unit>;

public sealed class UnlinkSaleFromTradeCaseCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<UnlinkSaleFromTradeCaseCommand, Unit>
{
    public async Task<Unit> Handle(UnlinkSaleFromTradeCaseCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.TradeCases.Edit))
            throw new ForbiddenException("Loyihani tahrirlashga ruxsat yo'q.");
        var tradeCase = await db.TradeCases
            .FromSqlInterpolated($"SELECT * FROM trade_cases WHERE id = {request.CaseId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Trade case not found.", "trade_case_not_found");
        if (tradeCase.Status is TradeCaseStatus.Settled or TradeCaseStatus.Cancelled)
            throw new ConflictException("Yakunlangan loyihadan savdo ajratib bo'lmaydi.", "trade_case_not_editable");
        if (request.ExpectedVersion.HasValue && request.ExpectedVersion != tradeCase.Version)
            throw new ConflictException("Loyiha boshqa qurilmada o'zgartirilgan.", "trade_case_version_conflict");

        var sale = await db.Sales
            .FromSqlInterpolated($"SELECT * FROM sales WHERE id = {request.SaleId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sale not found.", "sale_not_found");
        if (sale.TradeCaseId != tradeCase.Id)
            throw new ConflictException("Savdo bu loyihaga biriktirilmagan.", "sale_not_linked");
        var isSettlementSale = await db.TradeCaseSettlements.AnyAsync(
            x => x.TradeCaseId == tradeCase.Id && x.SaleId == sale.Id, cancellationToken);
        if (isSettlementSale)
            throw new BusinessRuleException("Hisob-kitob savdosini loyihadan ajratib bo'lmaydi.", "settlement_sale_not_unlinkable");

        sale.TradeCaseId = null;
        tradeCase.Version++;
        await db.SaveChangesAsync(cancellationToken);
        audit.SetOutcome("case.sale_unlinked", "trade_cases", tradeCase.Id, new
        {
            tradeCase.CaseNumber,
            saleId = sale.Id,
            sale.ReceiptToken,
            tradeCase.Version
        }, "Savdo loyihadan ajratildi", tradeCase.BranchId);
        return Unit.Value;
    }
}

public sealed class UnlinkSaleFromTradeCaseCommandValidator : AbstractValidator<UnlinkSaleFromTradeCaseCommand>
{
    public UnlinkSaleFromTradeCaseCommandValidator()
    {
        RuleFor(x => x.CaseId).GreaterThan(0);
        RuleFor(x => x.SaleId).GreaterThan(0);
    }
}
