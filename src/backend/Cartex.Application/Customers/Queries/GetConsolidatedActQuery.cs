using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Cartex.Shared.Models.Customers;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Customers.Queries;

/// DAL-04: the act reads and reports. It posts nothing, creates no document and changes no
/// balance — a customer asking "what did I actually use and what do I still owe" must never
/// have the answer alter the books.
public sealed record GetConsolidatedActQuery(long CustomerId, List<ConsolidatedActSelection> Documents)
    : IRequest<ConsolidatedActDto>;

public sealed class GetConsolidatedActQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ISettingsService settings) : IRequestHandler<GetConsolidatedActQuery, ConsolidatedActDto>
{
    public async Task<ConsolidatedActDto> Handle(GetConsolidatedActQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Customers.Act))
            throw new ForbiddenException("Yig'ma dalolatnoma tuzishga ruxsat yo'q.");

        var policy = await settings.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken)
                     ?? new SalesPolicySettings();
        if (!policy.AllowConsolidatedAct)
            throw new BusinessRuleException("Yig'ma dalolatnoma o'chirilgan.", "consolidated_act_disabled");

        var customer = await db.Customers
            .Where(x => x.Id == request.CustomerId)
            .Select(x => new { x.Id, x.FullName, x.AssignedUserId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Customer not found.", "customer_not_found");
        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll)
            && customer.AssignedUserId != currentUser.UserId)
            throw new NotFoundException("Customer not found.", "customer_not_found");

        return new ConsolidatedActDto(
            customer.Id, customer.FullName,
            DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow),
            [], [], 0, 0, 0);
    }
}

public sealed class GetConsolidatedActQueryValidator : AbstractValidator<GetConsolidatedActQuery>
{
    public GetConsolidatedActQueryValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.Documents).NotEmpty();
    }
}
