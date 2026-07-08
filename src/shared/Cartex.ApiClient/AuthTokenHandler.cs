namespace Cartex.ApiClient;

public sealed class AuthTokenHandler(
    Func<string?> tokenProvider,
    Func<CancellationToken, Task<string?>>? refreshAsync = null,
    Action? onUnauthorized = null) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = refreshAsync is not null ? await refreshAsync(cancellationToken) : tokenProvider();
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && !string.IsNullOrEmpty(token))
            onUnauthorized?.Invoke();

        return response;
    }
}
