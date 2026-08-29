using System.Security.Cryptography;
using Cartex.Application.Common;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Auth.Services;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Store.Commands;

public record RequestStoreOtpCommand(string Phone) : IRequest<RequestOtpResponse>;

public record RequestOtpResponse(bool Sent);

public sealed class RequestStoreOtpCommandHandler(
    IApplicationDbContext db,
    IFeatureStateProvider features,
    IPasswordHasher passwordHasher,
    ISettingsService settings,
    ITelegramService telegram,
    ISmsService sms) : IRequestHandler<RequestStoreOtpCommand, RequestOtpResponse>
{
    public async Task<RequestOtpResponse> Handle(RequestStoreOtpCommand request, CancellationToken cancellationToken)
    {
        if (!await features.IsEnabledAsync(FeatureCatalog.Ordering, cancellationToken))
            throw new ForbiddenException("Onlayn buyurtma o'chirilgan.");

        var phone = Phones.Normalize(request.Phone);
        var customer = phone is null ? null : await db.Customers.Include(c => c.Party).FirstOrDefaultAsync(c => c.Party.Phone == phone, cancellationToken);

        // Uniform response regardless of whether the phone is a registered customer (no enumeration).
        if (customer is null)
            return new RequestOtpResponse(true);

        var now = DateTime.UtcNow;
        var recent = await db.OtpChallenges
            .AnyAsync(o => o.CustomerId == customer.Id && o.ConsumedAt == null && o.CreatedAt > now.AddSeconds(-60), cancellationToken);
        if (recent)
            return new RequestOtpResponse(true);

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        db.OtpChallenges.Add(new OtpChallenge
        {
            CustomerId = customer.Id,
            CodeHash = passwordHasher.Hash(code),
            ExpiresAt = now.AddMinutes(5)
        });

        await db.SaveChangesAsync(cancellationToken);
        await TrySendAsync(customer, $"Cartex: tasdiqlash kodi {code}. 5 daqiqa amal qiladi.", cancellationToken);
        return new RequestOtpResponse(true);
    }

    private async Task TrySendAsync(Customer customer, string text, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(customer.TelegramChatId))
        {
            var tg = await settings.GetAsync<TelegramSettings>(SettingKeys.Telegram, cancellationToken);
            if (tg is { Enabled: true } && !string.IsNullOrWhiteSpace(tg.BotToken))
            {
                await telegram.SendMessageAsync(customer.TelegramChatId, text, cancellationToken);
                return;
            }
        }

        var sm = await settings.GetAsync<SmsSettings>(SettingKeys.Sms, cancellationToken);
        if (sm is { Enabled: true } && !string.IsNullOrWhiteSpace(customer.Party.Phone))
        {
            var branchId = await db.Accounts.Where(x => x.CustomerId == customer.Id && x.BranchId != null)
                .Select(x => x.BranchId).FirstOrDefaultAsync(cancellationToken);
            branchId ??= await db.Branches.Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken);
            await sms.SendAsync(customer.Party.Phone, text,
                new SmsSendContext(branchId, SmsGatewayJobKind.Manual, customer.Id, $"otp:{customer.Id}:{DateTime.UtcNow:yyyyMMddHHmm}"), cancellationToken);
        }
    }
}

public sealed class RequestStoreOtpCommandValidator : AbstractValidator<RequestStoreOtpCommand>
{
    public RequestStoreOtpCommandValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30);
    }
}
