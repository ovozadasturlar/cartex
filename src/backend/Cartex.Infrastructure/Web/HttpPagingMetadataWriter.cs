namespace Cartex.Infrastructure.Web;

using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using Cartex.Shared.Models.Common;

public class HttpPagingMetadataWriter(IHttpContextAccessor accessor) : IPagingMetadataWriter
{
    public void Write(PagedListMetadata metadata)
    {
        var headers = accessor.HttpContext?.Response.Headers;
        if (headers is null) return;
        headers["X-Paging"] = JsonSerializer.Serialize(metadata);
    }
}
