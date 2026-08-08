using Cartex.Application.Auth;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Store.Commands;

public record StoreLogoutCommand(string RefreshToken) : IRequest<Unit>;

public sealed class StoreLogoutCommandHandler(IApplicationDbContext db) : IRequestHandler<StoreLogoutCommand, Unit>
{
    public async Task<Unit> Handle(StoreLogoutCommand request, CancellationToken cancellationToken)
    {
        var hash = RefreshTokens.Hash(request.RefreshToken);
        var now = DateTime.UtcNow;
        await db.CustomerSessions
            .Where(s => s.TokenHash == hash && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), cancellationToken);
        return Unit.Value;
    }
}

public sealed class StoreLogoutCommandValidator : AbstractValidator<StoreLogoutCommand>
{
    public StoreLogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
