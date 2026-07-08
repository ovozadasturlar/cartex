using System;
using System.Linq;
using System.Text.Json;
using Refit;

namespace Cartex.UI.Services;

public static class ApiErrors
{
    public static string Describe(Exception ex)
    {
        if (ex is ApiException api && !string.IsNullOrEmpty(api.Content))
        {
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
