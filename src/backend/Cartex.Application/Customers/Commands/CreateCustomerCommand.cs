using Cartex.Application.Common;
using Cartex.Domain.Authorization;
using FluentValidation;
using Cartex.Persistence;
using Cartex.Application.Common.Finance;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Customers.Commands;

public record CreateCustomerCommand(string FullName, string? Phone, string? CardBarcode, decimal DiscountPct, string? Email = null, string? LastName = null, string? Address = null, decimal CreditLimit = 0, bool NotificationsOptOut = false, decimal OpeningBalance = 0, string? OpeningCurrency = null, string? PreferredLanguage = null, long? AssignedUserId = null, double? Latitude = null, double? Longitude = null, long? AgentId = null) : ICommand<long>;

public sealed class CreateCustomerCommandHandler(
    IApplicationDbContext db,
    ILedgerService ledger,
    ICurrencyService currency,
    ICurrentUser currentUser) : IRequestHandler<CreateCustomerCommand, long>
{
    public async Task<long> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var phone = Phones.Normalize(request.Phone);
        var businessId = currentUser.BusinessId
            ?? await db.Businesses.Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException("Business not found.");
        var party = phone is null
            ? null
            : await db.Parties.FirstOrDefaultAsync(x => x.BusinessId == businessId && x.Phone == phone,
                cancellationToken);
        if (party is not null && await db.Customers.AnyAsync(x => x.PartyId == party.Id, cancellationToken))
            throw new ConflictException("Bu telefon raqamli mijoz allaqachon mavjud.", "customer_already_exists");
        party ??= new Party
        {
            BusinessId = businessId,
            FullName = request.FullName.Trim(),
            Phone = phone,
            Email = NormalizeOptional(request.Email),
            Address = NormalizeOptional(request.Address)
        };
        if (party.Id == 0) db.Parties.Add(party);

        var customer = new Customer
        {
            Party = party,
            FullName = request.FullName,
            LastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            Phone = phone,
            Email = request.Email,
            CardBarcode = request.CardBarcode,
            DiscountPct = request.DiscountPct,
            CreditLimit = request.CreditLimit,
            NotificationsOptOut = request.NotificationsOptOut,
            PreferredLanguage = request.PreferredLanguage ?? "uz-latn",
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            // AgentId remains a wire-compatible alias for older clients. Internally this
            // relation is an assigned employee, never an external sales partner.
            AssignedUserId = currentUser.HasPermission(AppPermissions.Customers.ViewAll)
                ? request.AssignedUserId ?? request.AgentId
                : currentUser.UserId
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);

        if (request.OpeningBalance != 0)
        {
            var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
            var baseCode = await currency.BaseAsync(cancellationToken);
            var code = string.IsNullOrWhiteSpace(request.OpeningCurrency) ? baseCode : request.OpeningCurrency.Trim().ToUpperInvariant();
            await currency.EnsureSalesAllowedAsync(code, cancellationToken);

            var rate = code == baseCode ? 1m : await currency.RateAsync(code, cancellationToken);
            var amount = Math.Abs(request.OpeningBalance);
            Transaction tx;
            if (request.OpeningBalance > 0)
            {
                var debt = await ledger.CustomerAccountAsync(customer.Id, AccountType.Debt, cancellationToken, code);
                tx = ledger.Post(OperationType.DebtCharge, amount, null, debt, userId, null, rate);
            }
            else
            {
                var advance = await ledger.CustomerAccountAsync(customer.Id, AccountType.CustomerAdvance, cancellationToken, code);
                tx = ledger.Post(OperationType.CustomerAdvance, amount, null, advance, userId, null, rate);
            }
            tx.Description = "Boshlang'ich qoldiq";
            await db.SaveChangesAsync(cancellationToken);
        }

        return customer.Id;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty();
        RuleFor(x => x.Phone).NotEmpty().Must(p => Phones.IsValid(Phones.Normalize(p))).WithMessage("Telefon raqami noto'g'ri.");
        RuleFor(x => x.CreditLimit).GreaterThanOrEqualTo(0);
    }
}
