using Cartex.Shared.Models.Storage;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IStorageApi
{
    [Multipart]
    [Post("/api/storage/upload")]
    Task<UploadResult> UploadAsync(StreamPart file);

    [Multipart]
    [Post("/api/storage/upload-logo")]
    Task<LogoUploadResult> UploadLogoAsync(StreamPart file);

    [Post("/api/storage/from-url")]
    Task<UploadResult> UploadFromUrlAsync([Body] ImageFromUrlRequest request);

    [Get("/api/storage/url")]
    Task<ImageUrlResult> GetUrlAsync([Query] string key);
}
