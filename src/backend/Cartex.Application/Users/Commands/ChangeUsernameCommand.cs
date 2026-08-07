using Cartex.Application.Common.Messaging;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Users.Commands;

public record ChangeUsernameCommand(long UserId, string NewUsername) : ICommand<Unit>;

public sealed class ChangeUsernameCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<ChangeUsernameCommand, Unit>
{
    public async Task<Unit> Handle(ChangeUsernameCommand request, CancellationToken cancellationToken)
    {
        // Only wildcard (developer) access allowed
        if (!currentUser.CanAccessAllBranches && !currentUser.HasPermission(AppPermissions.Wildcard))
            throw new ForbiddenException("Only developers can change usernames.");

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        var taken = await db.Users
            .AnyAsync(u => u.Username == request.NewUsername && u.Id != request.UserId, cancellationToken);
        if (taken)
            throw new BusinessRuleException("Bu username allaqachon band.");

        var oldUsername = user.Username;
        user.Username = request.NewUsername;

        audit.Add("user.username_changed", "users", user.Id, new { OldUsername = oldUsername, NewUsername = request.NewUsername });

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class ChangeUsernameCommandValidator : AbstractValidator<ChangeUsernameCommand>
{
    public ChangeUsernameCommandValidator()
    {
        RuleFor(x => x.NewUsername).NotEmpty().MinimumLength(3).MaximumLength(50)
            .Matches("^[a-zA-Z0-9_.-]+$").WithMessage("Username faqat harf, raqam, _ . - belgilardan iborat bo'lishi kerak.");
    }
}
