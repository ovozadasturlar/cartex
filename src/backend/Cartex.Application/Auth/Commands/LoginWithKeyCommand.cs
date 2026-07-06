using Cartex.Application.Common.Messaging;
using FluentValidation;
using Cartex.Application.Common.Interfaces;

namespace Cartex.Application.Auth.Commands;

public record LoginWithKeyCommand(string KeyContent, string Serial) : IRequest<LoginResponse>;

public sealed class LoginWithKeyCommandHandler(
    AuthTokenBuilder tokenBuilder,
    IHardwareKeyService hardwareKeys) : IRequestHandler<LoginWithKeyCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(LoginWithKeyCommand request, CancellationToken cancellationToken)
    {
        var username = await hardwareKeys.VerifyAsync(request.KeyContent, request.Serial, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid hardware key.");

        var user = await tokenBuilder.LoadUserAsync(username, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid hardware key.");

        if (!user.IsActive)
            throw new ForbiddenException("User is deactivated.");

        return await tokenBuilder.BuildAsync(user, cancellationToken);
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
