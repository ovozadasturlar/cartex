using Cartex.Application.Common.Messaging;
using FluentValidation;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Auth.Commands;

public record LoginWithKeyCommand(string KeyContent, string Serial, string? DeviceName = null) : IRequest<LoginResponse>;

public sealed class LoginWithKeyCommandHandler(
    AuthTokenBuilder tokenBuilder,
    IHardwareKeyService hardwareKeys,
    ISettingsService settings,
    IApplicationDbContext db) : IRequestHandler<LoginWithKeyCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(LoginWithKeyCommand request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<LoginMethodsSettings>(SettingKeys.LoginMethods, cancellationToken) ?? new();
        if (!cfg.KeyEnabled)
            throw new ForbiddenException("USB kalit bilan kirish o'chirilgan.");

        var username = await hardwareKeys.VerifyAsync(request.KeyContent, request.Serial, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid hardware key.");

        var user = await tokenBuilder.LoadUserAsync(username, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid hardware key.");

        if (!user.IsActive)
            throw new ForbiddenException("User is deactivated.");

        var key = await db.HardwareKeys.FirstOrDefaultAsync(k => k.UserId == user.Id && k.Serial == request.Serial && k.RevokedAt == null, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid hardware key.");

        if (!key.IsEnabled)
            throw new ForbiddenException("Kalit o'chirilgan.");

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
