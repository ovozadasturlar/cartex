using Cartex.Application.Common.Messaging;
using FluentValidation;

namespace Cartex.Application.Auth.Commands;

public record RefreshTokenCommand(string RefreshToken, string? DeviceName = null) : IRequest<LoginResponse>;

public sealed class RefreshTokenCommandHandler(AuthTokenBuilder tokenBuilder)
    : IRequestHandler<RefreshTokenCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken) =>
        await tokenBuilder.RotateAsync(request.RefreshToken, request.DeviceName, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid or expired refresh token.");
}

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
