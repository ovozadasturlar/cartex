using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Interfaces;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Auth.Commands;

public record SetHardwareKeyEnabledCommand(long Id, bool Enabled) : ICommand<Unit>;

public sealed class SetHardwareKeyEnabledCommandHandler(IApplicationDbContext db, IAuditService audit) : IRequestHandler<SetHardwareKeyEnabledCommand, Unit>
{
    public async Task<Unit> Handle(SetHardwareKeyEnabledCommand request, CancellationToken cancellationToken)
    {
        var key = await db.HardwareKeys.Include(k => k.User).FirstOrDefaultAsync(k => k.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Kalit topilmadi.");
        if (key.RevokedAt is not null)
            throw new BusinessRuleException("Bekor qilingan kalitni o'zgartirib bo'lmaydi.");
        if (key.IsEnabled == request.Enabled)
            return Unit.Value;

        key.IsEnabled = request.Enabled;
        audit.Add(request.Enabled ? "hwkeyEnable" : "hwkeyDisable", "users", key.UserId, new { key.User.Username, key.Serial });
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
