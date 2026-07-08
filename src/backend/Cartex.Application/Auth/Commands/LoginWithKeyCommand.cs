using Cartex.Application.Common.Messaging;
using FluentValidation;
using Cartex.Application.Common.Interfaces;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Auth.Commands;

public record LoginWithKeyCommand(string KeyContent, string Serial, string? DeviceName = null) : IRequest<LoginResponse>;

public sealed class LoginWithKeyCommandHandler(
    AuthTokenBuilder tokenBuilder,
    IHardwareKeyService hardwareKeys,
    IApplicationDbContext db) : IRequestHandler<LoginWithKeyCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(LoginWithKeyCommand request, CancellationToken cancellationToken)
    {
        var username = await hardwareKeys.VerifyAsync(request.KeyContent, request.Serial, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid hardware key.");

        var user = await tokenBuilder.LoadUserAsync(username, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid hardware key.");

        if (!user.IsActive)
            throw new ForbiddenException("User is deactivated.");

        if (!await db.HardwareKeys.AnyAsync(k => k.UserId == user.Id && k.Serial == request.Serial && k.RevokedAt == null, cancellationToken))
            throw new UnauthorizedAccessException("Invalid hardware key.");

        return await tokenBuilder.IssueAsync(user, request.DeviceName, cancellationToken);
    }
}

public sealed class LoginWithKeyCommandValidator : AbstractValidator<LoginWithKeyCommand>
{
    public LoginWithKeyCommandValidator()
    {
        RuleFor(x => x.KeyContent).NotEmpty();
        RuleFor(x => x.Serial).NotEmpty();
    }
}
