using Cartex.Shared.Models.Storage;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IStorageApi
{
    [Multipart]
    [Post("/api/storage/upload")]
    Task<UploadResult> UploadAsync(StreamPart file);

    [Get("/api/storage/url")]
    Task<ImageUrlResult> GetUrlAsync([Query] string key);
}
