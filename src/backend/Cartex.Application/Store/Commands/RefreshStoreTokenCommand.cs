using FluentValidation;

namespace Cartex.Application.Store.Commands;

public record RefreshStoreTokenCommand(string RefreshToken, string? DeviceName = null) : IRequest<StoreLoginResponse>;

public sealed class RefreshStoreTokenCommandHandler(StoreTokenBuilder tokenBuilder)
    : IRequestHandler<RefreshStoreTokenCommand, StoreLoginResponse>
{
    public async Task<StoreLoginResponse> Handle(RefreshStoreTokenCommand request, CancellationToken cancellationToken) =>
        await tokenBuilder.RotateAsync(request.RefreshToken, request.DeviceName, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid or expired refresh token.");
}

public sealed class RefreshStoreTokenCommandValidator : AbstractValidator<RefreshStoreTokenCommand>
{
    public RefreshStoreTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
