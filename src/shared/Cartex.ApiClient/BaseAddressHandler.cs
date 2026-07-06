namespace Cartex.ApiClient;

public sealed class BaseAddressHandler(Func<string> baseUrlProvider) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is { } uri)
            request.RequestUri = new Uri(new Uri(baseUrlProvider().TrimEnd('/') + "/"), uri.PathAndQuery.TrimStart('/'));
        return base.SendAsync(request, cancellationToken);
    }
}
