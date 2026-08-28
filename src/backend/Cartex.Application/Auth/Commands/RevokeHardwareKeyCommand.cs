using Cartex.Application.Common.Security;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Auth.Commands;

public record RevokeHardwareKeyCommand(long Id) : ICommand<Unit>;

public sealed class RevokeHardwareKeyCommandHandler(
    IApplicationDbContext db,
    IAccessControlService accessControl,
    IAuditService audit) : IRequestHandler<RevokeHardwareKeyCommand, Unit>
{
    public async Task<Unit> Handle(RevokeHardwareKeyCommand request, CancellationToken cancellationToken)
    {
        var key = await db.HardwareKeys.Include(k => k.User).FirstOrDefaultAsync(k => k.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Kalit topilmadi.");

        await accessControl.EnsureCanManageUserAsync(key.User, cancellationToken);

        if (key.RevokedAt is not null)
            return Unit.Value;

        key.RevokedAt = DateTime.UtcNow;
        audit.Add("hwkeyRevoke", "users", key.UserId, new { key.User.Username, key.Serial });
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
