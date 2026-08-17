using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Common;
using Cartex.Persistence;
using FluentValidation;
using Cartex.Shared.Models.Auth;

namespace Cartex.Application.Auth.Commands;

public record StartQrLoginCommand : IRequest<QrLoginStartResponse>;

public record GetLoginMethodsQuery : IRequest<LoginMethodsDto>;

public record ApproveQrLoginCommand(string Code) : ICommand<Unit>;

public record PollQrLoginCommand(string Code, string? DeviceName = null, string? DeviceId = null) : IRequest<LoginResponse?>;

public sealed class GetLoginMethodsQueryHandler(ISettingsService settings) : IRequestHandler<GetLoginMethodsQuery, LoginMethodsDto>
{
    public async Task<LoginMethodsDto> Handle(GetLoginMethodsQuery request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<LoginMethodsSettings>(SettingKeys.LoginMethods, cancellationToken) ?? new();
        return new LoginMethodsDto(cfg.QrEnabled, cfg.KeyEnabled);
    }
}

public sealed class StartQrLoginCommandHandler(
    IQrLoginStore store,
    ISettingsService settings) : IRequestHandler<StartQrLoginCommand, QrLoginStartResponse>
{
    public async Task<QrLoginStartResponse> Handle(StartQrLoginCommand request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<LoginMethodsSettings>(SettingKeys.LoginMethods, cancellationToken) ?? new();
        if (!cfg.QrEnabled)
            throw new BusinessRuleException("QR bilan kirish o'chirilgan.");

        var seconds = Math.Clamp(cfg.QrRefreshSeconds, 30, 600);
        return new QrLoginStartResponse(store.Start(TimeSpan.FromSeconds(seconds)), seconds);
    }
}

public sealed class ApproveQrLoginCommandHandler(
    IQrLoginStore store,
    ICurrentUser currentUser,
    IAuditService audit,
    IApplicationDbContext db) : IRequestHandler<ApproveQrLoginCommand, Unit>
{
    public async Task<Unit> Handle(ApproveQrLoginCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        if (!store.Approve(request.Code, userId))
            throw new BusinessRuleException("QR kod eskirgan yoki noto'g'ri.");

        audit.Add("qrApprove", "auth", userId);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class ApproveQrLoginCommandValidator : AbstractValidator<ApproveQrLoginCommand>
{
    public ApproveQrLoginCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty();
    }
}

public sealed class PollQrLoginCommandHandler(
    IQrLoginStore store,
    AuthTokenBuilder tokenBuilder) : IRequestHandler<PollQrLoginCommand, LoginResponse?>
{
    public async Task<LoginResponse?> Handle(PollQrLoginCommand request, CancellationToken cancellationToken)
    {
        var userId = store.TakeApproved(request.Code);
        if (userId is null) return null;

        var user = await tokenBuilder.LoadUserByIdAsync(userId.Value, cancellationToken);
        if (user is null || !user.IsActive) return null;

        return await tokenBuilder.IssueAsync(user, request.DeviceName, request.DeviceId, cancellationToken);
    }
}
