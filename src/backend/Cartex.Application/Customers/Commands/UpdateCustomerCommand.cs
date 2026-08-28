using Cartex.Application.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Customers.Commands;

public record UpdateCustomerCommand(long Id, string FullName, string? Phone, string? CardBarcode, decimal DiscountPct, string? Email = null, string? LastName = null, string? Address = null, decimal? CreditLimit = null, bool NotificationsOptOut = false, string? PreferredLanguage = null, long? AssignedUserId = null, double? Latitude = null, double? Longitude = null, long? AgentId = null, string? Note = null, bool AllowMarketingSms = false, decimal? OpeningBalance = null, string? OpeningCurrency = null) : ICommand<Unit>;

public sealed class UpdateCustomerCommandHandler(
    IApplicationDbContext db,
    CustomerOpeningBalance opening,
    ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<UpdateCustomerCommand, Unit>
{
    public async Task<Unit> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll) && customer.AssignedUserId != currentUser.UserId)
            throw new NotFoundException("Customer not found.");

        customer.LastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName.Trim();
        customer.CardBarcode = request.CardBarcode;
        customer.DiscountPct = request.DiscountPct;
        customer.CreditLimit = request.CreditLimit;
        customer.NotificationsOptOut = request.NotificationsOptOut;
        customer.AllowMarketingSms = request.AllowMarketingSms;
        var party = await db.Parties.FirstAsync(x => x.Id == customer.PartyId, cancellationToken);
        party.FullName = request.FullName.Trim();
        party.Phone = Phones.Normalize(request.Phone);
        party.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        party.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        party.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (request.PreferredLanguage is not null)
            customer.PreferredLanguage = request.PreferredLanguage;
        var assignedUserId = request.AssignedUserId ?? request.AgentId;
        if (assignedUserId is not null)
            customer.AssignedUserId = assignedUserId == 0 ? null : assignedUserId;
        if (request.Latitude is not null && request.Longitude is not null)
        {
            customer.Latitude = request.Latitude == 0 ? null : request.Latitude;
            customer.Longitude = request.Latitude == 0 ? null : request.Longitude;
        }

        if (request.OpeningBalance is { } balance)
            await ReplaceOpeningBalanceAsync(customer.Id, balance, request.OpeningCurrency, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    // QARZ-24: tuzatish faqat hali hech qanday operatsiya bo'lmagan mijozda ochiq.
    private async Task ReplaceOpeningBalanceAsync(
        long customerId, decimal balance, string? currencyCode, CancellationToken cancellationToken)
    {
        // QARZ-23: tuzatish ham majburiyat yaratadi, shuning uchun o'sha ruxsat talab qilinadi.
        if (!currentUser.HasPermission(AppPermissions.Customers.OpeningBalance))
            throw new ForbiddenException(
                "Boshlang'ich qoldiqni o'zgartirishga ruxsat yo'q.", "opening_balance_forbidden");
        if (!await opening.IsUntouchedForWriteAsync(customerId, cancellationToken))
            throw new BusinessRuleException(
                "Operatsiya bajarilgan mijozning boshlang'ich qoldig'i o'zgartirilmaydi.", "customer_has_activity");

        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        var previous = await opening.ClearAsync(customerId, cancellationToken);
        await opening.PostAsync(customerId, balance, currencyCode, userId, cancellationToken);
        audit.Add("customer.opening_balance_changed", "customer", customerId,
            new { Previous = previous, Current = balance, Currency = currencyCode });
    }
}

public sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty();
        RuleFor(x => x.Phone).Must(p => Phones.IsValid(Phones.Normalize(p))).WithMessage("Telefon raqami noto'g'ri.")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.CreditLimit).GreaterThanOrEqualTo(0).When(x => x.CreditLimit is not null);
    }
}
