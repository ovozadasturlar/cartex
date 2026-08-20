using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cartex.Shared.Models.OfflineCache;

public static class HubRoles
{
    public const string Hub = "hub";
    public const string Satellite = "satellite";
}

/// HUB-04: qurilma guvohnoma so'raganda o'z ochiq kalitini (SPKI, base64) beradi — guvohnoma
/// shu kalitga bog'lanadi va faqat kalit egasi undan foydalana oladi.
public sealed record HubAttestationRequest(string PublicKey);

/// HUB-04: qurilma bulutdan olgan guvohnoma. `Token` — imzolangan payload
/// (`base64url(json).base64url(imzo)`), `PublicKey` esa uni oflaynda tekshirish uchun serverning
/// ochiq kaliti. `Epoch` — do'konning joriy vakolat versiyasi: yo'ldosh undan eski HUB'ga ulanmaydi
/// (`HUB-05`), shuning uchun u guvohnoma bilan bitta javobda keladi.
public sealed record HubAttestationDto(string PublicKey, string Token, DateTime ExpiresAt, long Epoch);

public sealed record HubAttestationPayload
{
    [JsonPropertyName("role")] public string Role { get; init; } = HubRoles.Satellite;
    [JsonPropertyName("biz")] public long BusinessId { get; init; }
    [JsonPropertyName("dev")] public string DeviceId { get; init; } = "";
    [JsonPropertyName("uid")] public long UserId { get; init; }
    [JsonPropertyName("wh")] public long WarehouseId { get; init; }
    [JsonPropertyName("lease")] public long LeaseId { get; init; }
    [JsonPropertyName("epoch")] public long Epoch { get; init; }
    [JsonPropertyName("exp")] public long ExpiresAtUnix { get; init; }

    /// Qurilmaning ochiq kaliti (SPKI, base64): TLS'da shu kalitga egalik isbotlanadi.
    [JsonPropertyName("pk")] public string DevicePublicKey { get; init; } = "";

    [JsonIgnore] public DateTime ExpiresAt => DateTimeOffset.FromUnixTimeSeconds(ExpiresAtUnix).UtcDateTime;
}

/// HUB-11: tarmoqqa e'lon qilinadigan qator. Guvohnoma bu yerda **yo'q** — e'lon har kimga
/// ochiq broadcast, guvohnoma esa faqat TLS ichida beriladi.
/// `Port` yetarli: manzilni qabul qiluvchi paketning jo'natuvchisidan oladi, shuning uchun
/// HUB o'z IP'sini bilishi shart emas (Androidda buni aniqlash ishonchsiz).
public sealed record HubBeaconDto(
    long BusinessId,
    long WarehouseId,
    long Epoch,
    string DeviceName,
    int Port);

public sealed record HubHelloDto(
    string Token,
    long WarehouseId,
    string WarehouseName,
    long Epoch,
    string DeviceName,
    bool AcceptsEvents);

public sealed record HubEventResult(
    Guid EventId,
    long Sequence,
    string Status,
    string? ErrorCode = null,
    string? Error = null);

public static class HubJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
