using System.Text.Json;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Cartex.Shared.Models.Settings;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Printing;

/// <summary>
/// Resolves the effective receipt template and writes automatic receipt jobs.
/// A PrintJob is itself the durable outbox record and is committed in the same
/// database transaction as the sale.
/// </summary>
public sealed class ReceiptPrintPolicyService(
    IApplicationDbContext db,
    ISettingsService settings,
    ICurrentUser currentUser)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<(ReceiptSettings Business, ReceiptSettings Effective)> ResolveAsync(
        PrintRoutingPolicy? policy,
        CancellationToken cancellationToken)
    {
        var business = await settings.GetAsync<ReceiptSettings>(SettingKeys.Receipt, cancellationToken) ?? new();
        var notification = await settings.GetAsync<NotificationSettings>(SettingKeys.Notification, cancellationToken);
        business.PublicReceiptBaseUrl = notification?.PublicBaseUrl;

        var effective = business;
        if (!string.IsNullOrWhiteSpace(policy?.ReceiptSettingsOverrideJson))
        {
            try
            {
                effective = JsonSerializer.Deserialize<ReceiptSettings>(policy.ReceiptSettingsOverrideJson, Json)
                    ?? business;
                effective.PublicReceiptBaseUrl = notification?.PublicBaseUrl;
            }
            catch (JsonException)
            {
                // A malformed legacy value must not stop checkout or printing.
                effective = business;
            }
        }

        return (business, effective);
    }

    public async Task<bool> EnqueueAutomaticReceiptAsync(Sale sale, CancellationToken cancellationToken)
    {
        var policy = await db.PrintRoutingPolicies.AsNoTracking()
            .FirstOrDefaultAsync(x => x.BranchId == sale.BranchId && x.Kind == PrintJobKind.Receipt,
                cancellationToken);
        if (policy is not { IsEnabled: true, AutoPrintOnSale: true })
            return false;

        var (_, receipt) = await ResolveAsync(policy, cancellationToken);
        var idempotencyKey = $"auto-receipt:{sale.ReceiptToken}";
        if (await db.PrintJobs.AnyAsync(x => x.BranchId == sale.BranchId
                && x.Kind == PrintJobKind.Receipt && x.IdempotencyKey == idempotencyKey,
                cancellationToken))
            return true;

        var deviceId = currentUser.DeviceId?.Trim();
        var originNodeId = string.IsNullOrWhiteSpace(deviceId)
            ? null
            : await db.PrintNodes.Where(x => x.BranchId == sale.BranchId && x.DeviceId == deviceId)
                .Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken);

        db.PrintJobs.Add(new PrintJob
        {
            BranchId = sale.BranchId,
            Kind = PrintJobKind.Receipt,
            Status = PrintJobStatus.Pending,
            SourceType = "receipt_token",
            SourceId = sale.ReceiptToken,
            PayloadJson = SerializeReceiptPayload(sale.ReceiptToken, receipt),
            IdempotencyKey = idempotencyKey,
            Copies = Math.Clamp(policy.DefaultCopies, 1, policy.MaxCopies),
            RequestedByUserId = sale.UserId,
            RequestedDeviceId = deviceId,
            RequestedDeviceName = currentUser.DeviceName,
            RequestedClient = currentUser.Client,
            RequestedIpAddress = currentUser.IpAddress,
            RequestedUserAgent = currentUser.UserAgent,
            CorrelationId = currentUser.CorrelationId,
            OriginNodeId = originNodeId
        });
        return true;
    }

    public static ReceiptSettingsDto ToDto(ReceiptSettings value) => new(
        value.HeaderText,
        value.FooterText,
        value.PaperWidth,
        value.PaperFormat,
        value.ShowBusinessName,
        value.ShowBranchName,
        value.ShowAddress,
        value.ShowPhone,
        value.ShowCashier,
        value.ShowCustomer,
        value.ShowReceiptNumber,
        value.ShowPaymentDetails,
        value.ShowQrCode,
        value.ShowElectronicLink,
        value.PublicReceiptBaseUrl,
        value.ShowLogo,
        value.ShowCustomerPhone,
        value.ShowCustomerEmail,
        value.Language ?? "uz-latn");

    public static ReceiptSettings FromDto(ReceiptSettingsDto value) => new()
    {
        HeaderText = Clean(value.HeaderText),
        FooterText = Clean(value.FooterText),
        PaperWidth = value.PaperWidth,
        PaperFormat = value.PaperFormat,
        ShowBusinessName = value.ShowBusinessName,
        ShowBranchName = value.ShowBranchName,
        ShowAddress = value.ShowAddress,
        ShowPhone = value.ShowPhone,
        ShowCashier = value.ShowCashier,
        ShowCustomer = value.ShowCustomer,
        ShowReceiptNumber = value.ShowReceiptNumber,
        ShowPaymentDetails = value.ShowPaymentDetails,
        ShowQrCode = value.ShowQrCode,
        ShowElectronicLink = value.ShowElectronicLink,
        ShowLogo = value.ShowLogo,
        ShowCustomerPhone = value.ShowCustomerPhone,
        ShowCustomerEmail = value.ShowCustomerEmail,
        Language = value.Language
    };

    public static string SerializeOverride(ReceiptSettingsDto value) =>
        JsonSerializer.Serialize(FromDto(value), Json);

    public static ReceiptSettingsDto? DeserializeOverride(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            var settings = JsonSerializer.Deserialize<ReceiptSettings>(value, Json);
            return settings is null ? null : ToDto(settings);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string SerializeReceiptPayload(string token, ReceiptSettings configured) =>
        JsonSerializer.Serialize(new { receiptToken = token, receiptSettings = Settings(configured) }, Json);

    public static string SerializeReturnPayload(long returnId, ReceiptSettings configured) =>
        JsonSerializer.Serialize(new { returnId, receiptSettings = Settings(configured) }, Json);

    public static object SettingsPayload(ReceiptSettings configured) => Settings(configured);

    private static object Settings(ReceiptSettings configured) =>
        new
        {
                headerText = configured.HeaderText,
                footerText = configured.FooterText,
                paperWidth = configured.PaperWidth,
                paperFormat = configured.PaperFormat,
                showBusinessName = configured.ShowBusinessName,
                showBranchName = configured.ShowBranchName,
                showAddress = configured.ShowAddress,
                showPhone = configured.ShowPhone,
                showCashier = configured.ShowCashier,
                showCustomer = configured.ShowCustomer,
                showReceiptNumber = configured.ShowReceiptNumber,
                showPaymentDetails = configured.ShowPaymentDetails,
                showQrCode = configured.ShowQrCode,
                showElectronicLink = configured.ShowElectronicLink,
                publicReceiptBaseUrl = configured.PublicReceiptBaseUrl,
                showLogo = configured.ShowLogo,
            showCustomerPhone = configured.ShowCustomerPhone,
            showCustomerEmail = configured.ShowCustomerEmail
        };

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
