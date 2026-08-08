using Cartex.Auth.Services;
using Cartex.Domain.Common;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Auth.Commands;

public record ChangePasswordCommand(string CurrentPassword, string NewPassword) : IRequest<Unit>;

public sealed class ChangePasswordCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IPasswordHasher passwordHasher) : IRequestHandler<ChangePasswordCommand, Unit>
{
    public async Task<Unit> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        var user = await db.Users.FirstAsync(u => u.Id == userId, cancellationToken);

        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid current password.");

        user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(6);
    }
}
