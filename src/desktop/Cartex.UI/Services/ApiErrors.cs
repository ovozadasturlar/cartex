using System.Net;
using System.Text.Json;
using Refit;

namespace Cartex.UI.Services;

public static class ApiErrors
{
    /// <summary>Sahifa almashganda bekor qilingan so'rov — bu xatolik emas, foydalanuvchiga ko'rsatilmaydi.</summary>
    public static bool IsCancelled(Exception ex) =>
        ex is OperationCanceledException || ex.InnerException is OperationCanceledException;

    private static bool IsUnreachable(Exception ex) =>
        ex is HttpRequestException
        || ex.InnerException is HttpRequestException or System.Net.Sockets.SocketException
        || (ex is TaskCanceledException && ex.InnerException is TimeoutException);

    public static string Describe(Exception ex)
    {
        if (IsUnreachable(ex)) return LocalizationManager.Instance["err_server_unreachable"];
        if (IsCancelled(ex)) return string.Empty;

        if (ex is ApiException api)
        {
            if (api.StatusCode == HttpStatusCode.Forbidden)
                return LocalizationManager.Instance["err_forbidden"];
            if ((int)api.StatusCode >= 500)
                return LocalizationManager.Instance["err_server_error"];
            if (string.IsNullOrEmpty(api.Content)) return ex.Message;

            try
            {
                using var doc = JsonDocument.Parse(api.Content);
                var root = doc.RootElement;

                if (root.TryGetProperty("errors", out var errors))
                {
                    if (errors.ValueKind == JsonValueKind.Array)
                    {
                        var messages = errors.EnumerateArray()
                            .Select(e => e.TryGetProperty("error", out var m) ? m.GetString() : null)
                            .Where(m => !string.IsNullOrWhiteSpace(m));
                        var joined = string.Join("; ", messages);
                        if (!string.IsNullOrWhiteSpace(joined)) return joined;
                    }
                    else if (errors.ValueKind == JsonValueKind.Object)
                    {
                        var messages = errors.EnumerateObject()
                            .SelectMany(p => p.Value.ValueKind == JsonValueKind.Array
                                ? p.Value.EnumerateArray().Select(v => v.GetString())
                                : [p.Value.GetString()])
                            .Where(m => !string.IsNullOrWhiteSpace(m));
                        var joined = string.Join("; ", messages);
                        if (!string.IsNullOrWhiteSpace(joined)) return joined;
                    }
                }

                if (root.TryGetProperty("title", out var title) && title.GetString() is { Length: > 0 } text)
                    return text == "feature_locked" ? LocalizationManager.Instance["feature_locked"] : text;
            }
            catch (JsonException) { }

            return api.Content!;
        }

        return ex.Message;
    }
}
