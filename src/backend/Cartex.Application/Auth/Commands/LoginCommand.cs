using Cartex.Application.Common.Messaging;
using FluentValidation;
using Cartex.Auth.Services;
using Cartex.Application.Common.Interfaces;

namespace Cartex.Application.Auth.Commands;

public record LoginCommand(string Username, string Password, string? DeviceName = null) : IRequest<LoginResponse>;

public record LoginResponse(string Token, string RefreshToken, string FullName, string Role);

public sealed class LoginCommandHandler(
    AuthTokenBuilder tokenBuilder,
    IPasswordHasher passwordHasher) : IRequestHandler<LoginCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await tokenBuilder.LoadUserAsync(request.Username, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid username or password.");

        if (!user.IsActive)
            throw new ForbiddenException("User is deactivated.");

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid username or password.");

        return await tokenBuilder.IssueAsync(user, request.DeviceName, cancellationToken);
    }
}

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}
