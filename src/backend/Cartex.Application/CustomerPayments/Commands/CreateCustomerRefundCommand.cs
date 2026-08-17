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
    string? IdempotencyKey = null) : ICommand<CustomerRefundCreatedDto>;

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
                .Select(x => new CustomerRefundCreatedDto(
                    x.Id, x.DocumentNumber, x.TotalBaseAmount, x.AdvanceBaseAmount, x.LoanBaseAmount))
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null) return existing;
        }

        var customer = await db.Customers.FirstOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.", "customer_not_found");
        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll)
            && customer.AssignedUserId != currentUser.UserId)
            throw new NotFoundException("Customer not found.", "customer_not_found");

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

        var policy = await settings.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken)
                     ?? new SalesPolicySettings();

        // QARZ-07/08: the customer's own money goes back first; only what is left over is a loan,
        // and a loan is a different door with its own switch, permission and ceiling.
        var advances = new Dictionary<string, Account?>(StringComparer.OrdinalIgnoreCase);
        var loans = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var fromAdvance = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in requestedByCurrency)
        {
            var account = await ledger.FindCustomerAccountAsync(
                request.CustomerId, AccountType.CustomerAdvance, cancellationToken, row.Key);
            var covered = Math.Min(row.Value, Math.Max(0, account?.Balance ?? 0));
            advances[row.Key] = covered > 0 ? account : null;
            fromAdvance[row.Key] = covered;
            if (row.Value > covered) loans[row.Key] = row.Value - covered;
        }

        if (loans.Count > 0)
        {
            if (!policy.AllowCustomerLoans)
                throw new BusinessRuleException("Mijozga qarzga pul berish o'chirilgan.", "customer_credit_insufficient");
            if (!currentUser.HasPermission(AppPermissions.Customers.Loan))
                throw new ForbiddenException("Mijozga qarzga pul berishga ruxsat yo'q.");

            var loanBase = 0m;
            foreach (var loan in loans) loanBase += Math.Round(loan.Value * rates[loan.Key], 2);

            // QARZ-17: the ceiling measures the lending, not the customer's own money coming back.
            if (policy.MaxCustomerLoan is { } maxLoan && loanBase > maxLoan)
                throw new BusinessRuleException(
                    $"Qarzga berish {policy.MaxCustomerLoan:N0} dan osha olmaydi.", "customer_loan_limit");

            // QARZ-18: unlike goods debt, this one is checked on the server — cash is leaving the till.
            if (customer.CreditLimit is { } creditLimit)
            {
                var debts = await db.Accounts
                    .Where(x => x.CustomerId == request.CustomerId && x.Type == AccountType.Debt && x.Balance > 0)
                    .Select(x => new { x.Currency, x.Balance })
                    .ToListAsync(cancellationToken);
                var debtBase = 0m;
                foreach (var debt in debts)
                    debtBase += Math.Round(debt.Balance * await currency.RateAsync(debt.Currency, cancellationToken), 2);
                if (debtBase + loanBase > creditLimit)
                    throw new BusinessRuleException(
                        $"Mijozning qarz chegarasi {creditLimit:N0} dan oshib ketadi.", "credit_limit_exceeded");
            }
        }

        var shiftId = await db.Shifts
            .Where(x => x.UserId == userId && x.BranchId == branchId && x.Status == ShiftStatus.Open)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (normalized.Any(x => x.Method == PaymentMethod.Cash) && shiftId is null && policy.ShiftPolicy != "Off")
            throw new BusinessRuleException("Naqd qaytarish uchun ochiq smena talab qilinadi.");

        var businessDate = request.BusinessDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var document = new CustomerRefundDocument
        {
            BranchId = branchId,
            CustomerId = request.CustomerId,
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
            payout.Description = document.DocumentNumber;

            // The tender is settled against the advance until it runs out, then against the debt,
            // so a payout that spans both leaves one posting of each rather than a mixed one.
            var advanceLeft = fromAdvance[input.Currency];
            var takenFromAdvance = Math.Min(input.Amount, advanceLeft);
            if (takenFromAdvance > 0)
            {
                fromAdvance[input.Currency] = advanceLeft - takenFromAdvance;
                document.AdvanceBaseAmount += Math.Round(takenFromAdvance * rate, 2);
                var advanceDebit = ledger.Post(OperationType.CustomerRefund, takenFromAdvance,
                    advances[input.Currency]!, null, userId, shiftId, rate);
                advanceDebit.CustomerRefundDocument = document;
                advanceDebit.Description = document.DocumentNumber;
            }

            var lent = input.Amount - takenFromAdvance;
            if (lent > 0)
            {
                document.LoanBaseAmount += Math.Round(lent * rate, 2);
                var debt = await ledger.CustomerAccountAsync(
                    request.CustomerId, AccountType.Debt, cancellationToken, input.Currency);
                var loanCharge = ledger.Post(OperationType.CustomerLoan, lent,
                    null, debt, userId, shiftId, rate);
                loanCharge.CustomerRefundDocument = document;
                loanCharge.Description = document.DocumentNumber;
            }
        }

        document.BalanceAfterBase = await CustomerBalance.NetAsync(db, currency, request.CustomerId, cancellationToken);

        db.CustomerRefundDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);

        audit.SetOutcome("customer.refund_paid", "customer_refund_documents", document.Id, new
        {
            document.DocumentNumber,
            document.CustomerId,
            document.TotalBaseAmount,
            tenders = document.Tenders.Select(x => new { x.Method, x.Currency, x.Amount, x.Rate, x.AmountBase }),
            document.AdvanceBaseAmount,
            document.LoanBaseAmount,
            document.Note
        }, document.LoanBaseAmount > 0 ? "Mijozga qarzga pul berildi" : "Mijozga avansdan pul qaytarildi", branchId);

        return new CustomerRefundCreatedDto(document.Id, document.DocumentNumber, document.TotalBaseAmount,
            document.AdvanceBaseAmount, document.LoanBaseAmount);
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
