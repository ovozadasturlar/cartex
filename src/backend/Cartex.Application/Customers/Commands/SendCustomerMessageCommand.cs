using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
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
    IAuditService audit) : IRequestHandler<SendCustomerMessageCommand, Unit>
{
    public async Task<Unit> Handle(SendCustomerMessageCommand request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        if (customer.NotificationsOptOut)
            throw new BusinessRuleException("Mijoz xabarlardan bosh tortgan.");

        switch (request.Channel.ToLowerInvariant())
        {
            case "telegram":
                if (string.IsNullOrWhiteSpace(customer.TelegramChatId))
                    throw new BusinessRuleException("Mijoz Telegramga ulanmagan.");
                var tg = await settings.GetAsync<TelegramSettings>(SettingKeys.Telegram, cancellationToken);
                if (tg is not { Enabled: true } || string.IsNullOrWhiteSpace(tg.BotToken))
                    throw new BusinessRuleException("Telegram sozlanmagan.");
                await telegram.SendMessageAsync(customer.TelegramChatId, request.Text, cancellationToken);
                break;
            case "sms":
                if (string.IsNullOrWhiteSpace(customer.Phone))
                    throw new BusinessRuleException("Mijoz telefoni kiritilmagan.");
                var sm = await settings.GetAsync<SmsSettings>(SettingKeys.Sms, cancellationToken);
                if (sm is not { Enabled: true })
                    throw new BusinessRuleException("SMS sozlanmagan.");
                await sms.SendAsync(customer.Phone, request.Text, cancellationToken);
                break;
            case "email":
                if (string.IsNullOrWhiteSpace(customer.Email))
                    throw new BusinessRuleException("Mijoz emaili kiritilmagan.");
                var em = await settings.GetAsync<EmailSettings>(SettingKeys.Email, cancellationToken);
                if (em is not { Enabled: true })
                    throw new BusinessRuleException("Email sozlanmagan.");
                await email.SendAsync(customer.Email, "Cartex", request.Text, cancellationToken);
                break;
            default:
                throw new BusinessRuleException("Noto'g'ri kanal.");
        }

        audit.Add("message", "customers", customer.Id, new { request.Channel, request.Text });
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
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
