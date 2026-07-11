using Cartex.Application.Rates.Commands;
using Cartex.Application.Rates.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequiresFeature(FeatureCatalog.Multicurrency)]
public class RatesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Rates.Manage)]
    public async Task<ActionResult<IReadOnlyCollection<RateDto>>> GetCurrent()
    {
        var result = await sender.Send(new GetCurrentRatesQuery());
        return Ok(result);
    }

    [HttpGet("{code}/history")]
    [HasPermission(AppPermissions.Rates.Manage)]
    public async Task<ActionResult<IReadOnlyCollection<RateDto>>> GetHistory(string code)
    {
        var result = await sender.Send(new GetRateHistoryQuery(code));
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.Rates.Manage)]
    public async Task<ActionResult<long>> Set(SetExchangeRateCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpGet("currencies")]
    public async Task<ActionResult<IReadOnlyCollection<CurrencyDto>>> GetCurrencies([FromQuery] bool onlyEnabled = false) =>
        Ok(await sender.Send(new GetCurrenciesQuery(onlyEnabled)));

    [HttpPost("currencies")]
    [HasPermission(AppPermissions.Currencies.Manage)]
    public async Task<ActionResult<long>> CreateCurrency(CreateCurrencyCommand command) =>
        Ok(await sender.Send(command));

    [HttpPut("currencies/{code}")]
    [HasPermission(AppPermissions.Currencies.Manage)]
    public async Task<IActionResult> UpdateCurrency(string code, UpdateCurrencyCommand command)
    {
        await sender.Send(command with { Code = code });
        return NoContent();
    }

    [HttpDelete("currencies/{code}")]
    [HasPermission(AppPermissions.Currencies.Manage)]
    public async Task<IActionResult> DeleteCurrency(string code)
    {
        await sender.Send(new DeleteCurrencyCommand(code));
        return NoContent();
    }
}
