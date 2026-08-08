using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Notifications.Commands;

public sealed record PlayMobileStatusItem(
    [property: JsonPropertyName("message-id")] string MessageId,
    string? Channel,
    string Status,
    [property: JsonPropertyName("status-date")] string? StatusDate,
    string? Description);

public record ProcessPlayMobileStatusCommand(
    string? Authorization,
    List<PlayMobileStatusItem> Messages) : ICommand<Unit>;

public sealed class ProcessPlayMobileStatusCommandHandler(
    ISettingsService settings,
    ISecretProtector protector,
    IApplicationDbContext db)
    : IRequestHandler<ProcessPlayMobileStatusCommand, Unit>
{
    public async Task<Unit> Handle(ProcessPlayMobileStatusCommand request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<SmsSettings>(SettingKeys.Sms, cancellationToken);
        if (cfg is null || !cfg.Enabled || !string.Equals(cfg.Provider, "playmobile", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("PlayMobile integratsiyasi faol emas.");

        var password = string.IsNullOrWhiteSpace(cfg.Password) ? "" : protector.Unprotect(cfg.Password);
        if (!ValidBasic(request.Authorization, cfg.Login ?? "", password))
            throw new UnauthorizedAccessException("PlayMobile callback autentifikatsiyasi noto'g'ri.");

        var ids = request.Messages.Select(x => x.MessageId).Distinct().ToList();
        var attempts = await db.NotificationDeliveryAttempts
            .Include(x => x.NotificationDelivery)
            .Where(x => x.Provider == "playmobile"
                && x.ProviderMessageId != null
                && ids.Contains(x.ProviderMessageId))
            .ToDictionaryAsync(x => x.ProviderMessageId!, cancellationToken);

        foreach (var item in request.Messages)
        {
            if (!attempts.TryGetValue(item.MessageId, out var attempt))
                continue;
            var status = Map(item.Status);
            if (status is null)
                continue;

            var at = DateTime.TryParse(item.StatusDate, out var parsed)
                ? parsed.ToUniversalTime()
                : DateTime.UtcNow;
            attempt.Status = status.Value;
            attempt.ErrorMessage = status is NotificationDeliveryStatus.Failed or NotificationDeliveryStatus.Undelivered
                ? Truncate(item.Description, 1000)
                : null;
            attempt.NotificationDelivery.Status = status.Value;

            if (status == NotificationDeliveryStatus.Accepted)
            {
                attempt.AcceptedAt ??= at;
                attempt.NotificationDelivery.AcceptedAt ??= at;
                continue;
            }

            attempt.CompletedAt = at;
            attempt.NotificationDelivery.CompletedAt = at;
            if (status == NotificationDeliveryStatus.Delivered)
            {
                attempt.DeliveredAt = at;
                attempt.NotificationDelivery.DeliveredAt = at;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    private static NotificationDeliveryStatus? Map(string status) =>
        status.ToUpperInvariant() switch
        {
            "TRANSMITTED" => NotificationDeliveryStatus.Accepted,
            "DELIVERED" => NotificationDeliveryStatus.Delivered,
            "NOTDELIVERED" or "REJECTED" or "EXPIRED" => NotificationDeliveryStatus.Undelivered,
            "FAILED" => NotificationDeliveryStatus.Failed,
            _ => null
        };

    private static bool ValidBasic(string? authorization, string login, string password)
    {
        if (string.IsNullOrWhiteSpace(authorization)
            || !authorization.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return false;
        try
        {
            var supplied = Convert.FromBase64String(authorization[6..].Trim());
            var expected = Encoding.UTF8.GetBytes($"{login}:{password}");
            return supplied.Length == expected.Length && CryptographicOperations.FixedTimeEquals(supplied, expected);
        }
        catch { return false; }
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}

public sealed class ProcessPlayMobileStatusCommandValidator : AbstractValidator<ProcessPlayMobileStatusCommand>
{
    public ProcessPlayMobileStatusCommandValidator()
    {
        RuleFor(x => x.Messages).NotEmpty().Must(x => x.Count <= 1000);
        RuleForEach(x => x.Messages).ChildRules(item =>
        {
            item.RuleFor(x => x.MessageId).NotEmpty().MaximumLength(160);
            item.RuleFor(x => x.Status).NotEmpty().MaximumLength(40);
            item.RuleFor(x => x.Description).MaximumLength(1000);
        });
    }
}
