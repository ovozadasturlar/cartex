using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Cartex.Catalog.Tool.Supabase;

public sealed class MasterCatalog : IDisposable
{
    public const string DefaultProject = "bvkcbcctttqbzvxidlus";
    public const string KeyVariable = "SUPABASE_SERVICE_KEY";

    private const int PageSize = 1000;

    private static readonly JsonSerializerOptions Format = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private readonly HttpClient rest;
    private readonly string secret;

    private MasterCatalog(HttpClient client, string key)
    {
        rest = client;
        secret = key;
    }

    public static MasterCatalog Open(string project)
    {
        var key = Environment.GetEnvironmentVariable(KeyVariable);
        if (string.IsNullOrWhiteSpace(key))
            throw new UnauthorizedAccessException(
                $"{KeyVariable} is not set. Put the service key of project '{project}' into that environment variable and run the command again; " +
                "the key is never taken as an argument and never written to a file.");

        var client = new HttpClient
        {
            BaseAddress = new Uri($"https://{project}.supabase.co/rest/v1/"),
            Timeout = TimeSpan.FromMinutes(5)
        };
        client.DefaultRequestHeaders.Add("apikey", key);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return new MasterCatalog(client, key);
    }

    public async Task<List<TRow>> Read<TRow>(string table, string select)
    {
        var rows = new List<TRow>();
        while (true)
        {
            var content = await Send(HttpMethod.Get, $"{table}?select={select}&order=id&limit={PageSize}&offset={rows.Count}", null, null);
            var page = JsonSerializer.Deserialize<List<TRow>>(content, Format)
                       ?? throw new InvalidDataException($"'{table}' returned no rows document.");

            rows.AddRange(page);
            if (page.Count < PageSize)
                return rows;
        }
    }

    public Task Write<TRow>(string table, IReadOnlyList<TRow> rows, string? conflict) =>
        Send(HttpMethod.Post,
            conflict is null ? table : $"{table}?on_conflict={conflict}",
            new StringContent(JsonSerializer.Serialize(rows, Format), Encoding.UTF8, "application/json"),
            conflict is null ? "return=minimal" : "resolution=merge-duplicates,return=minimal");

    public void Dispose() => rest.Dispose();

    private async Task<string> Send(HttpMethod method, string path, HttpContent? body, string? prefer)
    {
        using var request = new HttpRequestMessage(method, path) { Content = body };
        if (prefer is not null)
            request.Headers.Add("Prefer", prefer);

        using var response = await rest.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        return response.IsSuccessStatusCode
            ? content
            : throw new HttpRequestException(
                Redact($"{method} {path} failed with {(int)response.StatusCode} {response.ReasonPhrase}: {content}"));
    }

    private string Redact(string message) => message.Replace(secret, "<redacted>", StringComparison.Ordinal);
}
