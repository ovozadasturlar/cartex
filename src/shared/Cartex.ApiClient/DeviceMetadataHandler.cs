namespace Cartex.ApiClient;

public sealed class DeviceMetadataHandler(Func<string?>? deviceIdProvider, Func<string?>? deviceNameProvider) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var deviceId = deviceIdProvider?.Invoke();
        var deviceName = deviceNameProvider?.Invoke();
        if (!string.IsNullOrWhiteSpace(deviceId) && !request.Headers.Contains("X-Device-Id"))
            request.Headers.TryAddWithoutValidation("X-Device-Id", deviceId);
        if (!string.IsNullOrWhiteSpace(deviceName) && !request.Headers.Contains("X-Device-Name"))
            request.Headers.TryAddWithoutValidation("X-Device-Name", deviceName);
        return base.SendAsync(request, cancellationToken);
    }
}

