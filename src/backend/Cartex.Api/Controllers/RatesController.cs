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
}
