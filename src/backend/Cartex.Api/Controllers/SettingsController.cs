using Cartex.Application.Settings.Commands;
using Cartex.Application.Settings.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Shared.Models.Settings;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SmsMessageDto = Cartex.Application.Settings.Queries.SmsMessageDto;
using SmsStatsDto = Cartex.Application.Settings.Queries.SmsStatsDto;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SettingsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Settings.Integrations)]
    public async Task<ActionResult<Cartex.Application.Settings.Queries.SettingsDto>> Get() =>
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

    [HttpGet("sms/journal")]
    [HasPermission(AppPermissions.Settings.Integrations)]
    public async Task<ActionResult<IReadOnlyCollection<SmsMessageDto>>> GetSmsJournal([FromQuery] GetSmsJournalQuery query) =>
        Ok(await sender.Send(query));

    [HttpGet("sms/stats")]
    [HasPermission(AppPermissions.Settings.Integrations)]
    public async Task<ActionResult<SmsStatsDto>> GetSmsStats([FromQuery] GetSmsStatsQuery query) =>
        Ok(await sender.Send(query));

    [HttpPut("notification")]
    [HasPermission(AppPermissions.Settings.Integrations)]
    public async Task<IActionResult> UpdateNotification(UpdateNotificationSettingsCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    [HttpGet("receipt")]
    public async Task<ActionResult<Cartex.Application.Settings.Queries.ReceiptSettingsDto>> GetReceipt()
    {
        var result = await sender.Send(new GetReceiptSettingsQuery());
        return Ok(result);
    }

    [HttpPut("receipt")]
    [HasPermission(AppPermissions.Settings.Receipt)]
    public async Task<IActionResult> UpdateReceipt(UpdateReceiptSettingsCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    [HttpGet("login-methods")]
    [HasPermission(AppPermissions.Settings.Security)]
    public async Task<ActionResult<Cartex.Application.Settings.Queries.LoginMethodsSettingsDto>> GetLoginMethods()
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

    [HttpGet("sales-policy")]
    public async Task<ActionResult<Cartex.Application.Settings.Queries.SalesPolicyDto>> GetSalesPolicy()
    {
        var result = await sender.Send(new GetSalesPolicyQuery());
        return Ok(result);
    }

    [HttpPut("sales-policy")]
    [HasPermission(AppPermissions.Business.Manage)]
    public async Task<IActionResult> UpdateSalesPolicy(UpdateSalesPolicyCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    [HttpGet("storage")]
    [HasPermission(AppPermissions.Settings.Integrations)]
    public async Task<ActionResult<Cartex.Application.Settings.Queries.StorageSettingsDto>> GetStorage()
    {
        var result = await sender.Send(new GetStorageSettingsQuery());
        return Ok(result);
    }

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
    public async Task<ActionResult<Cartex.Application.Settings.Queries.CloudBridgeSettingsDto>> GetCloudBridge()
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
    [HasPermission(AppPermissions.Notifications.Manage)]
    public async Task<ActionResult<Cartex.Application.Settings.Queries.ReminderSettingsDto>> GetReminder()
    {
        var result = await sender.Send(new GetReminderSettingsQuery());
        return Ok(result);
    }

    [HttpPut("reminder")]
    [HasPermission(AppPermissions.Notifications.Manage)]
    public async Task<IActionResult> UpdateReminder(UpdateReminderSettingsCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }
}
