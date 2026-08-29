using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Sales;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.CustomerPayments.Commands;

/// <summary>
/// Reverses a posted customer payment with a storno rather than deleting it, so the ledger,
/// the customer balance and the audit trail stay consistent with how sales are corrected.
/// </summary>
public sealed record VoidCustomerPaymentCommand(long DocumentId, string Reason) : ICommand<Unit>;

public sealed class VoidCustomerPaymentCommandValidator : AbstractValidator<VoidCustomerPaymentCommand>
{
    public VoidCustomerPaymentCommandValidator()
    {
        RuleFor(x => x.DocumentId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class VoidCustomerPaymentCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ILedgerService ledger,
    ISaleCorrectionPolicy correctionPolicy,
    IAuditService audit) : IRequestHandler<VoidCustomerPaymentCommand, Unit>
{
    public async Task<Unit> Handle(VoidCustomerPaymentCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        if (!currentUser.HasPermission(AppPermissions.CustomerPayments.Void))
            throw new ForbiddenException("To'lovni bekor qilishga ruxsat yo'q.");

        var document = await db.CustomerPaymentDocuments
            .FromSqlInterpolated($"SELECT * FROM customer_payment_documents WHERE id = {request.DocumentId} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Payment not found.", "payment_not_found");
        if (!currentUser.CanAccessAllBranches && !currentUser.BranchIds.Contains(document.BranchId))
            throw new NotFoundException("Payment not found.", "payment_not_found");
        if (document.Status != BusinessDocumentStatus.Posted)
            throw new BusinessRuleException("To'lov allaqachon bekor qilingan.", "payment_not_voidable");

        var transactions = await db.Transactions
            .Where(x => x.CustomerPaymentDocumentId == document.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        await correctionPolicy.EnsureCanCorrectAsync(
            transactions.FirstOrDefault()?.ShiftId, document.CreatedAt, cancellationToken);

        // The exact mirror of every posting returns the ledger to its pre-payment state whatever
        // mix of cash, card, bonus or advance the payment was made of.
        foreach (var original in transactions)
        {
            var from = original.ToAccountId is { } toId ? await ledger.AccountAsync(toId, cancellationToken) : null;
            var to = original.FromAccountId is { } fromId ? await ledger.AccountAsync(fromId, cancellationToken) : null;
            if (from is null && to is null) continue;

            var reversal = await ledger.PostAsync(original.OperationType, original.Amount, from, to,
                userId, cancellationToken, original.ShiftId, original.Rate);
            reversal.CustomerPaymentDocumentId = document.Id;
            reversal.BranchId = original.BranchId;
            reversal.Description = $"VOID {document.DocumentNumber}";
        }

        document.Status = BusinessDocumentStatus.Voided;
        document.Note = string.IsNullOrWhiteSpace(document.Note)
            ? request.Reason.Trim()
            : $"{document.Note} | {request.Reason.Trim()}";
        await db.SaveChangesAsync(cancellationToken);

        audit.SetOutcome("customer_payment.voided", "customer_payment_documents", document.Id, new
        {
            document.DocumentNumber,
            document.BranchId,
            document.CustomerId,
            document.TotalBaseAmount,
            Reason = request.Reason.Trim()
        }, "To'lov bekor qilindi (storno)", document.BranchId);

        return Unit.Value;
    }
}
