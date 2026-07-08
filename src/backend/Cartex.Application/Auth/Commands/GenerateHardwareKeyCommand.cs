using Cartex.Application.Common.Messaging;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Security;

namespace Cartex.Application.Auth.Commands;

public record GenerateHardwareKeyCommand(long UserId, string Serial) : ICommand<HardwareKeyResult>;

public record HardwareKeyResult(string FileName, string Content);

public sealed class GenerateHardwareKeyCommandHandler(
    IApplicationDbContext db,
    IHardwareKeyService hardwareKeys,
    IAccessControlService accessControl,
    IAuditService audit) : IRequestHandler<GenerateHardwareKeyCommand, HardwareKeyResult>
{
    public async Task<HardwareKeyResult> Handle(GenerateHardwareKeyCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        await accessControl.EnsureCanManageUserAsync(user, cancellationToken);

        if (!user.IsActive)
            throw new BusinessRuleException("User is deactivated.");

        var content = await hardwareKeys.IssueAsync(user.Username, request.Serial, cancellationToken);

        var existing = await db.HardwareKeys
            .FirstOrDefaultAsync(k => k.UserId == user.Id && k.Serial == request.Serial && k.RevokedAt == null, cancellationToken);
        if (existing is null)
            db.HardwareKeys.Add(new Domain.Entities.HardwareKey { UserId = user.Id, Serial = request.Serial });

        audit.Add("hwkey", "users", user.Id, new { user.Username, request.Serial });
        await db.SaveChangesAsync(cancellationToken);

        return new HardwareKeyResult($"cartex-{user.Username}.key", content);
    }
}

public sealed class GenerateHardwareKeyCommandValidator : AbstractValidator<GenerateHardwareKeyCommand>
{
    public GenerateHardwareKeyCommandValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.Serial).NotEmpty();
    }
}
