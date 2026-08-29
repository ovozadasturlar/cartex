using System.Security.Cryptography;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Cartex.Shared.Models.OfflineCache;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.OfflineCache.Queries;

/// `PublicKey` — qurilmaning SPKI ochiq kaliti (base64). Guvohnoma shu kalitga bog'lanadi.
public sealed record GetHubAttestationQuery(string PublicKey) : ICommand<HubAttestationDto>;

public sealed class GetHubAttestationQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ISettingsService settings)
    : IRequestHandler<GetHubAttestationQuery, HubAttestationDto>
{
    // HUB-04: guvohnoma onlayn paytda yangilanadi, oyna esa uzoq oflayn qolgan do'kon uchun
    // yetarli bo'lishi kerak — lekin cheksiz emas, aks holda o'g'irlangan qurilma abadiy kirardi.
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public async Task<HubAttestationDto> Handle(GetHubAttestationQuery request, CancellationToken cancellationToken)
    {
        var businessId = currentUser.BusinessId
            ?? throw new UnauthorizedAccessException("Business context is missing.");
        var deviceId = currentUser.DeviceId;
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new BusinessRuleException("Qurilma belgisi yo'q.", "device_id_required");

        var authority = await db.OfflineAuthorityLeases.AsNoTracking()
            .Where(x => x.BusinessId == businessId && x.RevokedAt == null)
            .Select(x => new { x.Id, x.Epoch, x.WarehouseId, x.DeviceId })
            .FirstOrDefaultAsync(cancellationToken);
        var mine = authority is not null && string.Equals(authority.DeviceId, deviceId, StringComparison.Ordinal);

        using var key = await SigningKeyAsync(businessId, cancellationToken);
        var expiresAt = DateTime.UtcNow + Lifetime;
        var payload = new HubAttestationPayload
        {
            Role = mine ? HubRoles.Hub : HubRoles.Satellite,
            BusinessId = businessId,
            DeviceId = deviceId,
            UserId = currentUser.UserId ?? 0,
            WarehouseId = mine ? authority!.WarehouseId : 0,
            LeaseId = mine ? authority!.Id : 0,
            Epoch = mine ? authority!.Epoch : 0,
            ExpiresAtUnix = new DateTimeOffset(expiresAt).ToUnixTimeSeconds(),
            DevicePublicKey = request.PublicKey
        };

        // HUB-05: joriy vakolat `epoch`i shu yerda qaytadi. Alohida so'rov bilan olinsa, o'sha so'rov
        // yiqilgan qurilma `minEpoch = 0` bilan qolib, 30 kunlik eski guvohnomani qabul qilardi.
        return new HubAttestationDto(
            Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
            HubAttestation.Sign(key, payload),
            expiresAt,
            authority?.Epoch ?? 0);
    }

    /// Kalit har o'rnatma uchun bitta va birinchi murojaatda yaratiladi. U bazada saqlanadi:
    /// bazaga kirish huquqi bo'lgan tomon do'kon ma'lumotiga baribir ega, shuning uchun bu
    /// xavf doirasini kengaytirmaydi; ayni paytda o'rnatish qo'shimcha sozlama talab qilmaydi.
    private async Task<ECDsa> SigningKeyAsync(long businessId, CancellationToken cancellationToken)
    {
        if (await StoredKeyAsync(cancellationToken) is { } existing) return existing;

        // Ikki parallel so'rov ikki xil kalit yaratsa, keyingisi avvalgisini bosib, allaqachon
        // berilgan guvohnomalarni yaroqsiz qilib qo'yardi. Biznes qatori qulflanadi va kalit
        // qulf ichida qayta o'qiladi — kutgan so'rov yutgan kalitni oladi.
        await db.Businesses
            .FromSqlInterpolated($"SELECT * FROM businesses WHERE id = {businessId} FOR UPDATE")
            .Select(x => x.Id)
            .SingleAsync(cancellationToken);

        if (await StoredKeyAsync(cancellationToken) is { } created) return created;

        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        await settings.SetAsync(SettingKeys.HubAttestationKey,
            new HubAttestationKeySettings { PrivateKey = Convert.ToBase64String(key.ExportPkcs8PrivateKey()) },
            cancellationToken);
        return key;
    }

    private async Task<ECDsa?> StoredKeyAsync(CancellationToken cancellationToken)
    {
        var stored = await settings.GetAsync<HubAttestationKeySettings>(
            SettingKeys.HubAttestationKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(stored?.PrivateKey)) return null;

        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        key.ImportPkcs8PrivateKey(Convert.FromBase64String(stored.PrivateKey), out _);
        return key;
    }
}

public sealed class GetHubAttestationQueryValidator : AbstractValidator<GetHubAttestationQuery>
{
    public GetHubAttestationQueryValidator()
    {
        RuleFor(x => x.PublicKey)
            .NotEmpty()
            .MaximumLength(200)
            .Must(HubAttestation.IsPublicKey)
            .WithMessage("Qurilma kaliti noto'g'ri.");
    }
}
