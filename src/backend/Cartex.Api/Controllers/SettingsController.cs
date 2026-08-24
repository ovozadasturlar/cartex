using Cartex.Application.Settings.Commands;
using Cartex.Application.Settings.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Shared.Models.Settings;
using Cartex.Shared.Models.Products;
using Cartex.Application.Common.Messaging;
using Cartex.Application.ProductReference.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SettingsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Settings.Integrations)]
    public async Task<ActionResult<SettingsDto>> Get() =>
        Ok(await sender.Send(new GetSettingsQuery()));

    [HttpPut("telegram")]
    [HasPermission(AppPermissions.Settings.Integrations)]
    public async Task<IActionResult> UpdateTelegram(UpdateTelegramSettingsCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    [HttpPost("telegram/test")]
    [HasPermission(AppPermissions.Settings.Integrations)]
    public async Task<ActionResult<TelegramTestResult>> TestTelegram(TelegramTestRequest request)
    {
        var info = await sender.Send(new TestTelegramQuery(request.BotToken));
        return Ok(new TelegramTestResult(info.Ok, info.Username));
    }

    [HttpPost("integrations/test")]
    [HasPermission(AppPermissions.Settings.Integrations)]
    public async Task<IActionResult> SendTestMessage(SendTestMessageCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    [HttpPut("email")]
    [HasPermission(AppPermissions.Settings.Integrations)]
    public async Task<IActionResult> UpdateEmail(UpdateEmailSettingsCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    [HttpPut("sms")]
    [HasPermission(AppPermissions.Settings.Integrations)]
    public async Task<IActionResult> UpdateSms(UpdateSmsSettingsCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    [HttpPut("notification")]
    [HasPermission(AppPermissions.Settings.Integrations)]
    public async Task<IActionResult> UpdateNotification(UpdateNotificationSettingsCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    [HttpGet("receipt")]
    public async Task<ActionResult<ReceiptSettingsDto>> GetReceipt()
    {
        var result = await sender.Send(new GetReceiptSettingsQuery());
        return Ok(result);
    }

    [HttpPost("receipt/preview")]
    [HasPermission(AppPermissions.Settings.Receipt)]
    public ActionResult<object> PreviewReceipt(ReceiptSettingsDto settings)
    {
        var receipt = new Cartex.Shared.Models.Sales.ReceiptDto(
            "preview-1048",
            "Cartex Market",
            "Chilonzor",
            "Toshkent shahri",
            null,
            new DateTime(2026, 8, 24, 14, 30, 0),
            84_000,
            0,
            84_000,
            0,
            0,
            0,
            0,
            0,
            "Akmal",
            [
                new Cartex.Shared.Models.Sales.ReceiptItemDto(
                    Cartex.Shared.Localization.ReceiptTexts.Get("sample_item_one", settings.Language),
                    2,
                    Cartex.Shared.Localization.ReceiptTexts.Get("unit_piece", settings.Language),
                    12_500,
                    25_000),
                new Cartex.Shared.Models.Sales.ReceiptItemDto(
                    Cartex.Shared.Localization.ReceiptTexts.Get("sample_item_two", settings.Language),
                    1,
                    Cartex.Shared.Localization.ReceiptTexts.Get("unit_piece", settings.Language),
                    8_000,
                    8_000),
                new Cartex.Shared.Models.Sales.ReceiptItemDto(
                    Cartex.Shared.Localization.ReceiptTexts.Get("sample_item_three", settings.Language),
                    1.5m,
                    Cartex.Shared.Localization.ReceiptTexts.Get("unit_piece", settings.Language),
                    14_000,
                    21_000),
                new Cartex.Shared.Models.Sales.ReceiptItemDto(
                    Cartex.Shared.Localization.ReceiptTexts.Get("sample_item_five", settings.Language),
                    2,
                    Cartex.Shared.Localization.ReceiptTexts.Get("unit_piece", settings.Language),
                    15_000,
                    30_000)
            ],
            [new Cartex.Shared.Models.Sales.ReceiptPaymentDto("Cash", "UZS", 84_000, 1, 84_000)],
            1048,
            "Dilshod",
            "+998 90 555 12 34",
            null,
            settings.Language,
            "+998 71 200 00 00");
        var options = new Cartex.Application.Common.Settings.ReceiptSettings
        {
            HeaderText = settings.HeaderText,
            FooterText = settings.FooterText,
            PaperWidth = settings.PaperWidth,
            PaperFormat = settings.PaperFormat,
            ShowBusinessName = settings.ShowBusinessName,
            ShowBranchName = settings.ShowBranchName,
            ShowAddress = settings.ShowAddress,
            ShowPhone = settings.ShowPhone,
            ShowCashier = settings.ShowCashier,
            ShowCustomer = settings.ShowCustomer,
            ShowReceiptNumber = settings.ShowReceiptNumber,
            ShowPaymentDetails = settings.ShowPaymentDetails,
            ShowQrCode = settings.ShowQrCode,
            ShowElectronicLink = settings.ShowElectronicLink,
            ShowLogo = settings.ShowLogo,
            ShowCustomerPhone = settings.ShowCustomerPhone,
            ShowCustomerEmail = settings.ShowCustomerEmail,
            Language = settings.Language,
            PublicReceiptBaseUrl = settings.PublicReceiptBaseUrl
        };
        return Ok(new { text = Cartex.Infrastructure.Notifications.ReceiptTextRenderer.Render(receipt, options) });
    }

    [HttpPut("receipt")]
    [HasPermission(AppPermissions.Settings.Receipt)]
    public async Task<IActionResult> UpdateReceipt(UpdateReceiptSettingsCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    [HttpGet("proforma")]
    public async Task<ActionResult<ProformaSettingsDto>> GetProforma() =>
        Ok(await sender.Send(new GetProformaSettingsQuery()));

    [HttpPut("proforma")]
    [HasPermission(AppPermissions.Settings.Receipt)]
    public async Task<IActionResult> UpdateProforma(UpdateProformaSettingsCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    [HttpGet("barcode-label")]
    [HasPermission(AppPermissions.Printing.BarcodePrint)]
    public async Task<ActionResult<BarcodeLabelSettingsDto>> GetBarcodeLabel() =>
        Ok(await sender.Send(new GetBarcodeLabelSettingsQuery()));

    [HttpPut("barcode-label")]
    [HasPermission(AppPermissions.Settings.BarcodeLabel)]
    public async Task<IActionResult> UpdateBarcodeLabel(UpdateBarcodeLabelSettingsCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    [HttpGet("login-methods")]
    [HasPermission(AppPermissions.Settings.Security)]
    public async Task<ActionResult<LoginMethodsSettingsDto>> GetLoginMethods()
    {
        var result = await sender.Send(new GetLoginMethodsSettingsQuery());
        return Ok(result);
    }

    [HttpPut("login-methods")]
    [HasPermission(AppPermissions.Settings.Security)]
    public async Task<IActionResult> UpdateLoginMethods(UpdateLoginMethodsSettingsCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    // Readable by any signed-in user on purpose: every seller's till needs the discount ceiling
    // and the debt rules to behave the same way the server will. Writing is the owner's call.
    [HttpGet("sales-policy")]
    public async Task<ActionResult<SalesPolicyDto>> GetSalesPolicy()
    {
        var result = await sender.Send(new GetSalesPolicyQuery());
        return Ok(result);
    }

    [HttpPut("sales-policy")]
    [HasPermission(AppPermissions.Settings.SalesPolicy)]
    public async Task<IActionResult> UpdateSalesPolicy(SalesPolicyDto policy)
    {
        await sender.Send(new UpdateSalesPolicyCommand(policy));
        return NoContent();
    }

    [HttpGet("storage")]
    [HasPermission(AppPermissions.Settings.Integrations)]
    public async Task<ActionResult<StorageSettingsDto>> GetStorage()
    {
        var result = await sender.Send(new GetStorageSettingsQuery());
        return Ok(result);
    }

    [HttpGet("product-reference")]
    [HasPermission(AppPermissions.Settings.SalesPolicy)]
    public async Task<ActionResult<ProductReferenceSettingsDto>> GetProductReferenceSettings() =>
        Ok(await sender.Send(new GetProductReferenceSettingsQuery()));

    [HttpPut("product-reference")]
    [HasPermission(AppPermissions.Settings.SalesPolicy)]
    public async Task<IActionResult> UpdateProductReferenceSettings(ProductReferenceSettingsDto settings)
    {
        await sender.Send(new UpdateProductReferenceSettingsCommand(settings));
        return NoContent();
    }

    [HttpPost("product-reference/sync")]
    [HasPermission(AppPermissions.Settings.SalesPolicy)]
    public async Task<ActionResult<ProductReferenceSyncResultDto>> SyncProductReference() =>
        Ok(await sender.Send(new SyncProductReferenceCommand()));

    [HttpPut("storage")]
    [HasPermission(AppPermissions.Settings.Integrations)]
    public async Task<IActionResult> UpdateStorage(UpdateStorageSettingsCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    [HttpPost("storage/migrate")]
    [HasPermission(AppPermissions.Settings.Integrations)]
    public async Task<IActionResult> StartStorageMigration(StartStorageMigrationCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    [HttpGet("storage/migrate")]
    [HasPermission(AppPermissions.Settings.Integrations)]
    public async Task<ActionResult<Cartex.Application.Common.Interfaces.StorageMigrationStatus>> GetStorageMigration()
    {
        var result = await sender.Send(new GetStorageMigrationStatusQuery());
        return Ok(result);
    }

    [HttpGet("cloud-bridge")]
    [HasPermission(AppPermissions.Settings.Integrations)]
    public async Task<ActionResult<CloudBridgeSettingsDto>> GetCloudBridge()
    {
        var result = await sender.Send(new GetCloudBridgeSettingsQuery());
        return Ok(result);
    }

    [HttpPut("cloud-bridge")]
    [HasPermission(AppPermissions.Settings.Integrations)]
    public async Task<IActionResult> UpdateCloudBridge(UpdateCloudBridgeSettingsCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    [HttpGet("reminder")]
    [HasPermission(AppPermissions.Notifications.View)]
    public async Task<ActionResult<ReminderSettingsDto>> GetReminder()
    {
        var result = await sender.Send(new GetReminderSettingsQuery());
        return Ok(result);
    }

    [HttpPut("reminder")]
    [HasPermission(AppPermissions.Notifications.Edit)]
    public async Task<IActionResult> UpdateReminder(UpdateReminderSettingsCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }
}
