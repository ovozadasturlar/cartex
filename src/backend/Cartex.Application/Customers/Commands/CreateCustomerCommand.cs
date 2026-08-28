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

public record CreateCustomerCommand(string FullName, string? Phone, string? CardBarcode, decimal DiscountPct, string? Email = null, string? LastName = null, string? Address = null, decimal? CreditLimit = null, bool NotificationsOptOut = false, decimal OpeningBalance = 0, string? OpeningCurrency = null, string? PreferredLanguage = null, long? AssignedUserId = null, double? Latitude = null, double? Longitude = null, long? AgentId = null, string? Note = null, bool AllowMarketingSms = false) : ICommand<long>;

public sealed class CreateCustomerCommandHandler(
    IApplicationDbContext db,
    CustomerOpeningBalance opening,
    ICurrentUser currentUser) : IRequestHandler<CreateCustomerCommand, long>
{
    public async Task<long> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        // QARZ-23: qoldiq defterga yozadi — mijoz yaratishdan alohida ruxsat talab qiladi.
        if (request.OpeningBalance != 0 && !currentUser.HasPermission(AppPermissions.Customers.OpeningBalance))
            throw new ForbiddenException(
                "Boshlang'ich qoldiq kiritishga ruxsat yo'q.", "opening_balance_forbidden");

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
        party ??= new Party { BusinessId = businessId, Phone = phone };
        if (party.Id == 0) db.Parties.Add(party);
        // Shaxsning o'zi haqidagi ma'lumot (ism, aloqa, izoh) faqat Party'da yashaydi: bitta odam
        // ham mijoz, ham hamkor bo'lishi mumkin va u har ikkalasida bir xil ko'rinishi kerak.
        party.FullName = request.FullName.Trim();
        party.Email = NormalizeOptional(request.Email);
        party.Address = NormalizeOptional(request.Address);
        if (!string.IsNullOrWhiteSpace(request.Note)) party.Note = request.Note.Trim();

        var customer = new Customer
        {
            Party = party,
            LastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName.Trim(),
            CardBarcode = request.CardBarcode,
            DiscountPct = request.DiscountPct,
            CreditLimit = request.CreditLimit,
            NotificationsOptOut = request.NotificationsOptOut,
            AllowMarketingSms = request.AllowMarketingSms,
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
            await opening.PostAsync(customer.Id, request.OpeningBalance, request.OpeningCurrency, userId, cancellationToken);
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
        RuleFor(x => x.CreditLimit).GreaterThanOrEqualTo(0).When(x => x.CreditLimit is not null);
    }
}
