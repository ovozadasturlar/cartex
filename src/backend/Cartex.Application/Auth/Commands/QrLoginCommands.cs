using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Domain.Common;
using Cartex.Persistence;
using FluentValidation;

namespace Cartex.Application.Auth.Commands;

public record StartQrLoginCommand : IRequest<QrLoginStartResponse>;

public record QrLoginStartResponse(string Code);

public record ApproveQrLoginCommand(string Code) : ICommand<Unit>;

public record PollQrLoginCommand(string Code, string? DeviceName = null) : IRequest<LoginResponse?>;

public sealed class StartQrLoginCommandHandler(IQrLoginStore store) : IRequestHandler<StartQrLoginCommand, QrLoginStartResponse>
{
    public Task<QrLoginStartResponse> Handle(StartQrLoginCommand request, CancellationToken cancellationToken) =>
        Task.FromResult(new QrLoginStartResponse(store.Start()));
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

        return await tokenBuilder.IssueAsync(user, request.DeviceName, cancellationToken);
    }
}
