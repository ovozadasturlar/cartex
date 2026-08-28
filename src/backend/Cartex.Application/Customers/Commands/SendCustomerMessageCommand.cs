using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Customers.Commands;

public record SendCustomerMessageCommand(long CustomerId, string Channel, string Text) : IRequest<Unit>;

public sealed class SendCustomerMessageCommandHandler(
    IApplicationDbContext db,
    ISettingsService settings,
    ITelegramService telegram,
    ISmsService sms,
    IEmailService email,
    IAuditService audit,
    Cartex.Domain.Common.ICurrentUser currentUser) : IRequestHandler<SendCustomerMessageCommand, Unit>
{
    public async Task<Unit> Handle(SendCustomerMessageCommand request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.Include(c => c.Party).FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        if (customer.NotificationsOptOut)
            throw new BusinessRuleException("Mijoz xabarlardan bosh tortgan.");

        var telegramSettings = await settings.GetAsync<TelegramSettings>(SettingKeys.Telegram, cancellationToken);
        var smsSettings = await settings.GetAsync<SmsSettings>(SettingKeys.Sms, cancellationToken);
        var emailSettings = await settings.GetAsync<EmailSettings>(SettingKeys.Email, cancellationToken);

        var telegramReady = telegramSettings is { Enabled: true } && !string.IsNullOrWhiteSpace(telegramSettings.BotToken);
        var smsReady = smsSettings is { Enabled: true };
        var emailReady = emailSettings is { Enabled: true };

        var channel = request.Channel.Trim().ToLowerInvariant();
        if (channel is "auto")
            channel = telegramReady && !string.IsNullOrWhiteSpace(customer.TelegramChatId) ? "telegram"
                : smsReady && !string.IsNullOrWhiteSpace(customer.Party.Phone) ? "sms"
                : emailReady && !string.IsNullOrWhiteSpace(customer.Party.Email) ? "email"
                : throw new BusinessRuleException(
                    "Bu mijozga xabar yuboradigan yoqilgan kanal yo'q.", "no_message_channel");

        switch (channel)
        {
            case "telegram":
                Require(!string.IsNullOrWhiteSpace(customer.TelegramChatId), "Mijoz Telegramga ulanmagan.");
                Require(telegramReady, "Telegram sozlanmagan.");
                await telegram.SendMessageAsync(customer.TelegramChatId!, request.Text, cancellationToken);
                break;
            case "sms":
                Require(!string.IsNullOrWhiteSpace(customer.Party.Phone), "Mijoz telefoni kiritilmagan.");
                Require(smsReady, "SMS sozlanmagan.");
                await sms.SendAsync(customer.Party.Phone!, request.Text,
                    new SmsSendContext(currentUser.DefaultBranchId, Cartex.Domain.Enums.SmsGatewayJobKind.Manual,
                        customer.Id, $"manual:{Guid.NewGuid():N}"), cancellationToken);
                break;
            case "email":
                Require(!string.IsNullOrWhiteSpace(customer.Party.Email), "Mijoz emaili kiritilmagan.");
                Require(emailReady, "Email sozlanmagan.");
                await email.SendAsync(customer.Party.Email!, "Cartex", request.Text, cancellationToken);
                break;
            default:
                throw new BusinessRuleException("Noto'g'ri kanal.");
        }

        audit.Add("message", "customers", customer.Id, new { Channel = channel, TextLength = request.Text.Length });
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    private static void Require(bool satisfied, string message)
    {
        if (!satisfied) throw new BusinessRuleException(message);
    }
}

public sealed class SendCustomerMessageCommandValidator : AbstractValidator<SendCustomerMessageCommand>
{
    public SendCustomerMessageCommandValidator()
    {
        RuleFor(x => x.Channel).NotEmpty();
        RuleFor(x => x.Text).NotEmpty().MaximumLength(1000);
    }
}
