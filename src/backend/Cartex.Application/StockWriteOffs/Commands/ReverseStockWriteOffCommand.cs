using Cartex.Application.Common.Documents;
using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Persistence.Services;
using Cartex.Shared.Models.Stocks;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.StockWriteOffs.Commands;

public sealed record ReverseStockWriteOffCommand(
    long DocumentId,
    string? Note = null,
    string? IdempotencyKey = null) : ICommand<StockWriteOffCreatedDto>;

public sealed class ReverseStockWriteOffCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ILedgerService ledger,
    ISettingsService settings,
    IAuditService audit,
    InventoryReasonState inventoryReason) : IRequestHandler<ReverseStockWriteOffCommand, StockWriteOffCreatedDto>
{
    public Task<StockWriteOffCreatedDto> Handle(ReverseStockWriteOffCommand request, CancellationToken cancellationToken) =>
        db.ExecuteInTransactionAsync(() => HandleCoreAsync(request, cancellationToken), cancellationToken);

    private async Task<StockWriteOffCreatedDto> HandleCoreAsync(ReverseStockWriteOffCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        await WriteOffAccess.EnsureAllowedAsync(currentUser, settings, cancellationToken);

        var original = (await db.LockAsync<StockWriteOffDocument>(
                $"SELECT * FROM stock_write_off_documents WHERE id = {request.DocumentId} FOR UPDATE", cancellationToken))
            .FirstOrDefault() ?? throw new NotFoundException("Write-off not found.", "write_off_not_found");

        if (!currentUser.CanAccessAllBranches && !currentUser.BranchIds.Contains(original.BranchId))
            throw new NotFoundException("Write-off not found.", "write_off_not_found");

        if (original.ReversesDocumentId is not null)
            throw new BusinessRuleException("Teskari chiqimni qaytarib bo'lmaydi.", "write_off_is_reversal");
        if (await db.StockWriteOffDocuments.AnyAsync(x => x.ReversesDocumentId == original.Id, cancellationToken))
            throw new BusinessRuleException("Bu chiqim allaqachon qaytarilgan.", "write_off_already_reversed");

        var idempotencyKey = WriteOffDocuments.Normalize(request.IdempotencyKey);
        if (idempotencyKey is not null)
        {
            var existing = await db.StockWriteOffDocuments
                .Where(x => x.BranchId == original.BranchId && x.IdempotencyKey == idempotencyKey)
                .Select(x => new StockWriteOffCreatedDto(x.Id, x.DocumentNumber, x.TotalCost,
                    x.Lines.Where(line => line.Disposition == InventoryDisposition.SupplierClaim).Sum(line => line.LineCost)))
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
                return existing;
        }

        var lines = await db.StockWriteOffLines.AsNoTracking()
            .Where(x => x.StockWriteOffDocumentId == original.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var stockIds = lines.Select(x => x.StockId).Distinct().Order().ToArray();
        var batches = (await db.LockAsync<Stock>(
                $"SELECT * FROM stocks WHERE id = ANY({stockIds}) ORDER BY id FOR UPDATE", cancellationToken))
            .ToDictionary(x => x.Id);

        var businessDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var document = new StockWriteOffDocument
        {
            BranchId = original.BranchId,
            WarehouseId = original.WarehouseId,
            UserId = userId,
            DocumentNumber = await DocumentNumbers.NextAsync(db, "WOF", businessDate, cancellationToken),
            BusinessDate = businessDate,
            ReversesDocumentId = original.Id,
            Note = WriteOffDocuments.Normalize(request.Note),
            IdempotencyKey = idempotencyKey
        };

        foreach (var line in lines)
            document.Lines.Add(new StockWriteOffLine
            {
                VariantId = line.VariantId,
                Stock = batches.TryGetValue(line.StockId, out var batch)
                    ? batch
                    : throw new NotFoundException("Stock batch not found.", "stock_batch_not_found"),
                SupplierId = line.SupplierId,
                Quantity = -line.Quantity,
                UnitCost = line.UnitCost,
                LineCost = -line.LineCost,
                ClaimCurrency = line.ClaimCurrency,
                ClaimRate = line.ClaimRate,
                Reason = line.Reason,
                Disposition = line.Disposition,
                Note = line.Note
            });

        document.TotalCost = -original.TotalCost;

        db.StockWriteOffDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);

        await WriteOffDocuments.MoveStockAsync(db, inventoryReason, document, cancellationToken);
        await WriteOffDocuments.PostSupplierClaimsAsync(ledger, document, userId, cancellationToken);
        var claimAmount = WriteOffDocuments.ClaimAmount(document.Lines);

        audit.SetOutcome("stock.write_off_reversed", "stock_write_off_documents", document.Id, new
        {
            document.DocumentNumber,
            document.WarehouseId,
            document.TotalCost,
            claimAmount,
            reversed = original.DocumentNumber
        }, "Chiqim teskari amal bilan tuzatildi", document.BranchId);
        await db.SaveChangesAsync(cancellationToken);

        return new StockWriteOffCreatedDto(document.Id, document.DocumentNumber, document.TotalCost, claimAmount);
    }
}

public sealed class ReverseStockWriteOffCommandValidator : AbstractValidator<ReverseStockWriteOffCommand>
{
    public ReverseStockWriteOffCommandValidator()
    {
        RuleFor(x => x.DocumentId).GreaterThan(0);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.IdempotencyKey).MaximumLength(128);
    }
}
