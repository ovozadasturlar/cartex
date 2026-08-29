using System.Net;
using System.Net.Http.Headers;

namespace Cartex.ApiClient;

public sealed class AuthTokenHandler(
    Func<string?> tokenProvider,
    Func<CancellationToken, Task<string?>>? refreshAsync = null,
    Func<CancellationToken, Task<string?>>? forceRefreshAsync = null,
    Action? onUnauthorized = null) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is not null)
            await request.Content.LoadIntoBufferAsync(cancellationToken);

        var token = refreshAsync is not null ? await refreshAsync(cancellationToken) : tokenProvider();
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);
        UpdateClock(response);

        if (response.StatusCode != HttpStatusCode.Unauthorized || string.IsNullOrEmpty(token))
            return response;

        var refreshed = forceRefreshAsync is null ? null : await forceRefreshAsync(cancellationToken);
        if (string.IsNullOrEmpty(refreshed) || string.Equals(refreshed, token, StringComparison.Ordinal))
        {
            onUnauthorized?.Invoke();
            return response;
        }

        using var retry = await CloneAsync(request, cancellationToken);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed);
        response.Dispose();
        response = await base.SendAsync(retry, cancellationToken);
        UpdateClock(response);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            onUnauthorized?.Invoke();

        return response;
    }

    private static void UpdateClock(HttpResponseMessage response)
    {
        if (response.Headers.Date is { } date)
            ServerClock.Update(date);
    }

    private static async Task<HttpRequestMessage> CloneAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (request.Content is null) return clone;
        clone.Content = new ByteArrayContent(await request.Content.ReadAsByteArrayAsync(cancellationToken));
        foreach (var header in request.Content.Headers)
            clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        return clone;
    }
}
