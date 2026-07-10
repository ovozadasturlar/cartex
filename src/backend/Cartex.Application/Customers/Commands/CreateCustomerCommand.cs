using Cartex.Application.Common;
using Cartex.Application.Common.Messaging;
using FluentValidation;
using Cartex.Persistence;
using Cartex.Application.Common.Finance;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;

namespace Cartex.Application.Customers.Commands;

public record CreateCustomerCommand(string FullName, string? Phone, string? CardBarcode, decimal DiscountPct, string? Email = null, string? LastName = null, string? Address = null, decimal CreditLimit = 0, bool NotificationsOptOut = false, decimal OpeningBalance = 0, string? OpeningCurrency = null, string? PreferredLanguage = null, long? AgentId = null, double? Latitude = null, double? Longitude = null) : ICommand<long>;

public sealed class CreateCustomerCommandHandler(
    IApplicationDbContext db,
    ILedgerService ledger,
    ICurrencyService currency,
    ICurrentUser currentUser) : IRequestHandler<CreateCustomerCommand, long>
{
    public async Task<long> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = new Customer
        {
            FullName = request.FullName,
            LastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            Phone = Phones.Normalize(request.Phone),
            Email = request.Email,
            CardBarcode = request.CardBarcode,
            DiscountPct = request.DiscountPct,
            CreditLimit = request.CreditLimit,
            NotificationsOptOut = request.NotificationsOptOut,
            PreferredLanguage = request.PreferredLanguage ?? "uz-latn",
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            AgentId = request.AgentId
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);

        if (request.OpeningBalance != 0)
        {
            var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
            var baseCode = await currency.BaseAsync(cancellationToken);
            var code = string.IsNullOrWhiteSpace(request.OpeningCurrency) ? baseCode : request.OpeningCurrency.Trim().ToUpperInvariant();
            await currency.EnsureAllowedAsync(code, cancellationToken);

            var rate = code == baseCode ? 1m : await currency.RateAsync(code, cancellationToken);
            var debt = await ledger.CustomerAccountAsync(customer.Id, AccountType.Debt, cancellationToken, code);
            var amount = Math.Abs(request.OpeningBalance);
            var tx = request.OpeningBalance > 0
                ? ledger.Post(OperationType.DebtCharge, amount, null, debt, userId, null, rate)
                : ledger.Post(OperationType.DebtCharge, amount, debt, null, userId, null, rate);
            tx.Description = "Boshlang'ich qoldiq";
            await db.SaveChangesAsync(cancellationToken);
        }

        return customer.Id;
    }
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
