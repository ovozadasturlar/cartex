using Cartex.Application.Common.Messaging;
using Cartex.Domain.Common;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Auth.Commands;

public record LogoutCommand(string RefreshToken) : IRequest<Unit>;

public sealed class LogoutCommandHandler(IApplicationDbContext db, IAuditService audit) : IRequestHandler<LogoutCommand, Unit>
{
    public async Task<Unit> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var hash = RefreshTokens.Hash(request.RefreshToken);
        var now = DateTime.UtcNow;
        var session = await db.RefreshSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TokenHash == hash && s.RevokedAt == null, cancellationToken);
        if (session is null)
            return Unit.Value;

        await db.RefreshSessions
            .Where(s => s.Id == session.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), cancellationToken);
        audit.Add("logout", "auth", session.UserId, new { session.DeviceName }, asUserId: session.UserId);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
