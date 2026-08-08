using Cartex.Domain.Common;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Store.Commands;

public record SetStoreLanguageCommand(string Language) : IRequest<Unit>;

public sealed class SetStoreLanguageCommandHandler(IApplicationDbContext db, ICurrentCustomer currentCustomer)
    : IRequestHandler<SetStoreLanguageCommand, Unit>
{
    public async Task<Unit> Handle(SetStoreLanguageCommand request, CancellationToken cancellationToken)
    {
        var customerId = currentCustomer.CustomerId ?? throw new UnauthorizedAccessException("Not authenticated.");
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Not authenticated.");
        customer.PreferredLanguage = request.Language;
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class SetStoreLanguageCommandValidator : AbstractValidator<SetStoreLanguageCommand>
{
    private static readonly string[] Allowed = ["uz-latn", "uz-cyrl", "ru", "en"];

    public SetStoreLanguageCommandValidator()
    {
        RuleFor(x => x.Language).Must(Allowed.Contains).WithMessage("Til noto'g'ri.");
    }
}
