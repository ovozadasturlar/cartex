using Cartex.Application.Common.Messaging;
using Cartex.Persistence;
using FluentValidation;

namespace Cartex.Application.Notifications.Commands;

public record RecordNotificationExportCommand(
    DateTime? From,
    DateTime? To,
    string? Channel,
    string? Provider,
    string? Status,
    string? Purpose,
    string Format,
    int RowCount) : ICommand<Unit>;

public sealed class RecordNotificationExportCommandHandler(IAuditService audit, IApplicationDbContext db)
    : IRequestHandler<RecordNotificationExportCommand, Unit>
{
    public async Task<Unit> Handle(RecordNotificationExportCommand request, CancellationToken cancellationToken)
    {
        audit.Add("Export", "NotificationDelivery", null, new
        {
            request.From,
            request.To,
            request.Channel,
            request.Provider,
            request.Status,
            request.Purpose,
            request.Format,
            request.RowCount
        });
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class RecordNotificationExportCommandValidator : AbstractValidator<RecordNotificationExportCommand>
{
    public RecordNotificationExportCommandValidator()
    {
        RuleFor(x => x.Format).NotEmpty().MaximumLength(10);
        RuleFor(x => x.RowCount).GreaterThanOrEqualTo(0);
    }
}
