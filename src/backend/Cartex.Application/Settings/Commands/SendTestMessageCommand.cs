using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;

namespace Cartex.Application.Settings.Commands;

public record SendTestMessageCommand(string Channel, string? Recipient) : IRequest<Unit>;

public sealed class SendTestMessageCommandHandler(
    ISettingsService settings,
    ITelegramService telegram,
    IEmailService email,
    ISmsService sms,
    Cartex.Domain.Common.ICurrentUser currentUser) : IRequestHandler<SendTestMessageCommand, Unit>
{
    public async Task<Unit> Handle(SendTestMessageCommand request, CancellationToken cancellationToken)
    {
        var text = $"Cartex test xabari — integratsiya to'g'ri sozlangan. {DateTime.UtcNow:dd.MM.yyyy HH:mm} UTC";
        var recipient = request.Recipient?.Trim();

        try
        {
            switch (request.Channel?.ToLowerInvariant())
            {
                case "telegram":
                    var tg = await settings.GetAsync<TelegramSettings>(SettingKeys.Telegram, cancellationToken);
                    if (tg is null || !tg.Enabled || string.IsNullOrWhiteSpace(tg.BotToken))
                        throw new BusinessRuleException("Telegram sozlanmagan yoki o'chiq — token kiritib saqlang");
                    recipient = string.IsNullOrWhiteSpace(recipient) ? tg.ChatId : recipient;
                    if (string.IsNullOrWhiteSpace(recipient))
                        throw new BusinessRuleException("Chat ID kiritilmagan");
                    await telegram.SendMessageAsync(recipient, text, cancellationToken);
                    break;

                case "email":
                    var em = await settings.GetAsync<EmailSettings>(SettingKeys.Email, cancellationToken);
                    if (em is null || !em.Enabled || string.IsNullOrWhiteSpace(em.Host) || string.IsNullOrWhiteSpace(em.FromAddress))
                        throw new BusinessRuleException("Email sozlanmagan yoki o'chiq — SMTP ma'lumotlarini kiritib saqlang");
                    if (string.IsNullOrWhiteSpace(recipient))
                        throw new BusinessRuleException("Qabul qiluvchi email kiritilmagan");
                    await email.SendAsync(recipient, "Cartex — test xabari", text, cancellationToken);
                    break;

                case "sms":
                    var sm = await settings.GetAsync<SmsSettings>(SettingKeys.Sms, cancellationToken);
                    if (sm is null || !sm.Enabled)
                        throw new BusinessRuleException("SMS sozlanmagan yoki o'chiq — provayder ma'lumotlarini kiritib saqlang");
                    if (string.IsNullOrWhiteSpace(recipient))
                        throw new BusinessRuleException("Qabul qiluvchi telefon raqami kiritilmagan");
                    await sms.SendAsync(recipient, text,
                        new SmsSendContext(currentUser.DefaultBranchId, Cartex.Domain.Enums.SmsGatewayJobKind.Manual,
                            IdempotencyKey: $"test:{Guid.NewGuid():N}"), cancellationToken);
                    break;

                default:
                    throw new BusinessRuleException($"Noma'lum kanal: {request.Channel}");
            }
        }
        catch (Exception ex) when (ex is not DomainException)
        {
            throw new BusinessRuleException($"Yuborilmadi: {ex.Message}");
        }

        return Unit.Value;
    }
}
