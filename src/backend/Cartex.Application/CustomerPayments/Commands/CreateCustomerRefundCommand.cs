using Cartex.Application.Common.Documents;
using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Customers;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.CustomerPayments.Commands;

public sealed record CustomerRefundTenderInput(PaymentMethod Method, string Currency, decimal Amount);

public sealed record CreateCustomerRefundCommand(
    long CustomerId,
    long? BranchId,
    List<CustomerRefundTenderInput> Tenders,
    DateOnly? BusinessDate = null,
    string? Note = null,
    string? IdempotencyKey = null,
    long? TradeCaseId = null) : ICommand<CustomerRefundCreatedDto>;

public sealed class CreateCustomerRefundCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ILedgerService ledger,
    ICurrencyService currency,
    ISettingsService settings,
    IAuditService audit) : IRequestHandler<CreateCustomerRefundCommand, CustomerRefundCreatedDto>
{
    public async Task<CustomerRefundCreatedDto> Handle(
        CreateCustomerRefundCommand request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        if (!currentUser.HasPermission(AppPermissions.Customers.Refund))
            throw new ForbiddenException("Mijozga pul qaytarishga ruxsat yo'q.");

        var branchId = request.BranchId ?? currentUser.DefaultBranchId
            ?? throw new BusinessRuleException("Foydalanuvchi filiali aniqlanmadi.");
        if (!currentUser.CanAccessAllBranches && !currentUser.BranchIds.Contains(branchId))
            throw new ForbiddenException("Bu filialga ruxsat yo'q.");

        var idempotencyKey = NormalizeOptional(request.IdempotencyKey);
        if (idempotencyKey is not null)
        {
            var existing = await db.CustomerRefundDocuments
                .Where(x => x.BranchId == branchId && x.IdempotencyKey == idempotencyKey)
                .Select(x => new CustomerRefundCreatedDto(x.Id, x.DocumentNumber, x.TotalBaseAmount))
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null) return existing;
        }

        var customer = await db.Customers.FirstOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.", "customer_not_found");
        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll)
            && customer.AssignedUserId != currentUser.UserId)
            throw new NotFoundException("Customer not found.", "customer_not_found");

        if (request.TradeCaseId is { } tradeCaseId && !await db.TradeCases.AnyAsync(x =>
                x.Id == tradeCaseId && x.CustomerId == request.CustomerId && x.BranchId == branchId,
                cancellationToken))
            throw new BusinessRuleException("Pul qaytarish bog'lanayotgan loyiha mos emas.", "invalid_trade_case_refund");

        var baseCode = (await currency.BaseAsync(cancellationToken)).ToUpperInvariant();
        var normalized = request.Tenders.Select(x => new CustomerRefundTenderInput(
            x.Method,
            string.IsNullOrWhiteSpace(x.Currency) ? baseCode : x.Currency.Trim().ToUpperInvariant(),
            x.Amount)).ToList();
        if (normalized.Any(x => x.Method is PaymentMethod.Bonus))
            throw new BusinessRuleException("Bonus pul qaytarish usuli bo'la olmaydi.", "unsupported_payment_method");

        var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in normalized.Select(x => x.Currency).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await currency.EnsureSalesAllowedAsync(code, cancellationToken);
            rates[code] = await currency.RateAsync(code, cancellationToken);
        }

        var requestedByCurrency = normalized.GroupBy(x => x.Currency, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Sum(row => row.Amount), StringComparer.OrdinalIgnoreCase);
        var advances = new Dictionary<string, Account>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in requestedByCurrency)
        {
            var account = await ledger.FindCustomerAccountAsync(
                request.CustomerId, AccountType.CustomerAdvance, cancellationToken, row.Key)
                ?? throw new BusinessRuleException($"{row.Key} bo'yicha mijoz avansi mavjud emas.", "customer_credit_insufficient");
            if (row.Value > account.Balance)
                throw new BusinessRuleException($"{row.Key} bo'yicha mijoz avansi yetarli emas.", "customer_credit_insufficient");
            advances[row.Key] = account;
        }

        var shiftId = await db.Shifts
            .Where(x => x.UserId == userId && x.BranchId == branchId && x.Status == ShiftStatus.Open)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var policy = await settings.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken)
                     ?? new SalesPolicySettings();
        if (normalized.Any(x => x.Method == PaymentMethod.Cash) && shiftId is null && policy.ShiftPolicy != "Off")
            throw new BusinessRuleException("Naqd qaytarish uchun ochiq smena talab qilinadi.");

        var businessDate = request.BusinessDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var document = new CustomerRefundDocument
        {
            BranchId = branchId,
            CustomerId = request.CustomerId,
            TradeCaseId = request.TradeCaseId,
            UserId = userId,
            DocumentNumber = await DocumentNumbers.NextAsync(db, "CRF", businessDate, cancellationToken),
            BusinessDate = businessDate,
            Note = NormalizeOptional(request.Note),
            IdempotencyKey = idempotencyKey
        };

        foreach (var input in normalized)
        {
            var rate = rates[input.Currency];
            var amountBase = Math.Round(input.Amount * rate, 2);
            var tender = new CustomerRefundTender
            {
                Method = input.Method,
                Currency = input.Currency,
                Amount = input.Amount,
                Rate = rate,
                AmountBase = amountBase
            };
            document.Tenders.Add(tender);
            document.TotalBaseAmount += amountBase;

            var payoutAccount = await ledger.BranchAccountAsync(
                branchId, AccountTypeFor(input.Method), cancellationToken, input.Currency);
            if (payoutAccount.Balance < input.Amount)
                throw new BusinessRuleException(
                    $"{input.Currency} bo'yicha qaytarish uchun mablag' yetarli emas.",
                    "insufficient_refund_funds");

            var payout = ledger.Post(OperationType.CustomerRefund, input.Amount,
                payoutAccount, null, userId, shiftId, rate);
            payout.CustomerRefundDocument = document;
            payout.TradeCaseId = request.TradeCaseId;
            payout.Description = document.DocumentNumber;

            var advanceDebit = ledger.Post(OperationType.CustomerRefund, input.Amount,
                advances[input.Currency], null, userId, shiftId, rate);
            advanceDebit.CustomerRefundDocument = document;
            advanceDebit.TradeCaseId = request.TradeCaseId;
            advanceDebit.Description = document.DocumentNumber;
        }

        db.CustomerRefundDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);

        audit.SetOutcome("customer.refund_paid", "customer_refund_documents", document.Id, new
        {
            document.DocumentNumber,
            document.CustomerId,
            document.TradeCaseId,
            document.TotalBaseAmount,
            tenders = document.Tenders.Select(x => new { x.Method, x.Currency, x.Amount, x.Rate, x.AmountBase }),
            document.Note
        }, "Mijozga avansdan pul qaytarildi", branchId);

        return new CustomerRefundCreatedDto(document.Id, document.DocumentNumber, document.TotalBaseAmount);
    }

    private static AccountType AccountTypeFor(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => AccountType.Cash,
        PaymentMethod.Card => AccountType.Card,
        PaymentMethod.Transfer => AccountType.Transfer,
        PaymentMethod.Bank => AccountType.Bank,
        _ => throw new BusinessRuleException("Qo'llab-quvvatlanmaydigan qaytarish turi.", "unsupported_payment_method")
    };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class CreateCustomerRefundCommandValidator : AbstractValidator<CreateCustomerRefundCommand>
{
    public CreateCustomerRefundCommandValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.Tenders).NotEmpty().Must(x => x.Count <= 20);
        RuleForEach(x => x.Tenders).ChildRules(row =>
        {
            row.RuleFor(x => x.Amount).GreaterThan(0);
            row.RuleFor(x => x.Currency).MaximumLength(3);
        });
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.IdempotencyKey).MaximumLength(128);
    }
}
