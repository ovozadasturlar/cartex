using Cartex.Application.Settings.Commands;
using Cartex.Application.Settings.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Shared.Models.Settings;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SettingsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Settings.Manage)]
    public async Task<IActionResult> Get() =>
        Ok(await sender.Send(new GetSettingsQuery()));

    [HttpPut("telegram")]
    [HasPermission(AppPermissions.Settings.Manage)]
    public async Task<IActionResult> UpdateTelegram(UpdateTelegramSettingsCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    [HttpPost("telegram/test")]
    [HasPermission(AppPermissions.Settings.Manage)]
    public async Task<IActionResult> TestTelegram(TelegramTestRequest request)
    {
        var info = await sender.Send(new TestTelegramQuery(request.BotToken));
        return Ok(new TelegramTestResult(info.Ok, info.Username));
    }

    [HttpPost("integrations/test")]
    [HasPermission(AppPermissions.Settings.Manage)]
    public async Task<IActionResult> SendTestMessage(SendTestMessageCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    [HttpPut("email")]
    [HasPermission(AppPermissions.Settings.Manage)]
    public async Task<IActionResult> UpdateEmail(UpdateEmailSettingsCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    [HttpPut("sms")]
    [HasPermission(AppPermissions.Settings.Manage)]
    public async Task<IActionResult> UpdateSms(UpdateSmsSettingsCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    [HttpPut("notification")]
    [HasPermission(AppPermissions.Settings.Manage)]
    public async Task<IActionResult> UpdateNotification(UpdateNotificationSettingsCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    [HttpGet("reminder")]
    [HasPermission(AppPermissions.Notifications.Manage)]
    public async Task<IActionResult> GetReminder()
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
