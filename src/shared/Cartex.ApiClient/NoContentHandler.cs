using System.Net;
using System.Text;

namespace Cartex.ApiClient;

public sealed class NoContentHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            response.StatusCode = HttpStatusCode.OK;
            response.Content = new StringContent("null", Encoding.UTF8, "application/json");
        }
        return response;
    }
}
