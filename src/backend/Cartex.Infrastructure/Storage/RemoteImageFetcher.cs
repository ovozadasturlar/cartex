using Cartex.Application.Common.Interfaces;

namespace Cartex.Infrastructure.Storage;

public sealed class RemoteImageFetcher : IRemoteImageFetcher
{
    private const long MaxBytes = 5 * 1024 * 1024;

    private static readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(15) };

    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif"
    };

    public async Task<RemoteImage?> FetchAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return null;

        try
        {
            using var response = await _client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (!Extensions.TryGetValue(contentType, out var extension))
                return null;
            if (response.Content.Headers.ContentLength > MaxBytes)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > MaxBytes)
                    return null;
            }

            return new RemoteImage(buffer.ToArray(), contentType, extension);
        }
        catch
        {
            return null;
        }
    }
}
