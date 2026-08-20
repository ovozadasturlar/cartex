using System.Buffers;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Cartex.Shared.Models.OfflineCache;

namespace Cartex.Hub;

/// Uch marshrutli, faqat o'z klientlarimizga xizmat qiladigan tinglovchi. Tayyor server
/// olinmadi: `HttpListener` Windowsda lokal bo'lmagan manzil uchun URL ACL (admin) talab qiladi,
/// Kestrel esa Androidda qo'llab-quvvatlanmaydi — ikkalasi ham o'rnatishni og'irlashtiradi.
/// Ulanish o'zaro TLS bilan o'raladi: qo'l siqishning o'zi ikkala tomonning shaxsiy kalitga
/// egaligini isbotlaydi va kanalni shifrlaydi (`HUB-04`, `HUB-12`).
public sealed class HubServer(IHubBackend backend, HubTrust trust, X509Certificate2 certificate)
    : IAsyncDisposable
{
    public const int DefaultPort = 45654;

    private const int MaxHeaderBytes = 8 * 1024;
    private const int MaxBodyBytes = 1024 * 1024;
    private const int MaxConnections = 32;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private int _open;

    public int Port { get; private set; }
    public bool IsRunning => _loop is { IsCompleted: false };

    public void Start(int port = DefaultPort)
    {
        if (IsRunning) return;
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _cts = new CancellationTokenSource();
        _loop = AcceptLoopAsync(_listener, _cts.Token);
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;
        await _cts.CancelAsync();
        _listener?.Stop();
        if (_loop is not null)
            try { await _loop; } catch (OperationCanceledException) { }
        _listener = null;
        _cts.Dispose();
        _cts = null;
        _loop = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                return;
            }

            // Bir vaqtda ochiq ulanishlar cheklanadi: aks holda bitta qurilma tinglovchini
            // ochiq ulanishlar bilan to'ldirib, do'kon savdosini to'xtatib qo'yardi.
            if (Interlocked.Increment(ref _open) > MaxConnections)
            {
                Interlocked.Decrement(ref _open);
                client.Dispose();
                continue;
            }

            _ = HandleAsync(client, cancellationToken);
        }
    }

    private async Task HandleAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);
        using (client)
        {
            try
            {
                // HUB-12: xizmat faqat do'kon tarmog'iga — tashqi manzilga javob berilmaydi.
                if (client.Client.RemoteEndPoint is not IPEndPoint remote || !HubNetwork.IsLocal(remote.Address))
                    return;

                await using var stream = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
                await stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificate = certificate,
                    // Sertifikat talab qilinadi, lekin zanjiri tekshirilmaydi: ishonch guvohnomadagi
                    // ochiq kalitdan keladi va u marshrutda solishtiriladi.
                    ClientCertificateRequired = true,
                    RemoteCertificateValidationCallback = static (_, _, _, _) => true,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                }, timeout.Token);

                var request = await ReadRequestAsync(stream, timeout.Token);
                if (request is null)
                {
                    await WriteAsync(stream, 400, "{\"error\":\"bad_request\"}", timeout.Token);
                    return;
                }

                var (status, body) = await RouteAsync(
                    request, stream.RemoteCertificate as X509Certificate2, timeout.Token);
                await WriteAsync(stream, status, body, timeout.Token);
            }
            catch (Exception exception) when (exception is IOException or OperationCanceledException
                                                  or SocketException or AuthenticationException)
            {
            }
            finally
            {
                Interlocked.Decrement(ref _open);
            }
        }
    }

    private async Task<(int Status, string Body)> RouteAsync(
        HubRequest request, X509Certificate2? peer, CancellationToken cancellationToken)
    {
        var identity = backend.Identity;

        // HUB-05: HUB ruxsat tekshirmaydi — faqat guvohnoma haqiqiy, shu do'konniki va uni
        // ko'rsatgan tomon TLS'da guvohnomadagi kalitga egaligini isbotlaganmi. Ikkinchi shart
        // bo'lmasa tarmoqda ushlangan guvohnoma bilan kirib bo'lardi.
        if (trust.Verify(request.Attestation, identity.BusinessId) is not { } caller
            || !HubIdentityKey.Matches(peer, caller.DevicePublicKey))
            return (401, "{\"error\":\"attestation_invalid\"}");

        switch (request.Method, request.Path)
        {
            case ("GET", "/hub/hello"):
                return (200, Json(new HubHelloDto(identity.AttestationToken, identity.WarehouseId,
                    identity.WarehouseName, identity.Epoch, identity.DeviceName, true)));

            case ("GET", "/hub/catalog"):
                var since = DateTime.TryParse(request.Query, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
                    ? parsed.ToUniversalTime()
                    : (DateTime?)null;
                return (200, Json(await backend.CatalogAsync(since, cancellationToken)));

            case ("POST", "/hub/events"):
                var payload = JsonSerializer.Deserialize<OfflineSyncEventRequest>(request.Body, HubJson.Options);
                if (payload is null)
                    return (400, "{\"error\":\"bad_request\"}");
                return (200, Json(await backend.AcceptAsync(payload, caller, cancellationToken)));

            default:
                return (404, "{\"error\":\"not_found\"}");
        }
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value, HubJson.Options);

    private static async Task<HubRequest?> ReadRequestAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(MaxHeaderBytes);
        try
        {
            var read = 0;
            var headerEnd = -1;
            while (headerEnd < 0)
            {
                var count = await stream.ReadAsync(buffer.AsMemory(read, MaxHeaderBytes - read), cancellationToken);
                if (count == 0) return null;
                read += count;
                headerEnd = Find(buffer, read);
                if (headerEnd < 0 && read >= MaxHeaderBytes) return null;
            }

            var head = Encoding.UTF8.GetString(buffer, 0, headerEnd);
            var lines = head.Split("\r\n");
            var parts = lines[0].Split(' ');
            if (parts.Length < 2) return null;

            var target = parts[1];
            var mark = target.IndexOf('?');
            var path = mark < 0 ? target : target[..mark];
            var query = mark < 0 ? null : QueryValue(target[(mark + 1)..], "since");
            string? attestation = null;
            var length = 0;
            var chunked = false;
            foreach (var line in lines.Skip(1))
            {
                var colon = line.IndexOf(':');
                if (colon <= 0) continue;
                var name = line[..colon].Trim();
                var value = line[(colon + 1)..].Trim();
                if (name.Equals(HubHeaders.Attestation, StringComparison.OrdinalIgnoreCase)) attestation = value;
                else if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) int.TryParse(value, out length);
                else if (name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)) chunked = true;
            }

            // Bo'lakli (chunked) tana qo'llab-quvvatlanmaydi: uni jimgina bo'sh deb qabul qilish
            // savdoni ma'lumotsiz qoldirardi, shuning uchun so'rov ochiq rad etiladi.
            if (chunked || length is < 0 or > MaxBodyBytes) return null;
            var bodyStart = headerEnd + 4;
            var body = "";
            if (length > 0)
            {
                var bytes = new byte[length];
                var have = Math.Min(length, read - bodyStart);
                Array.Copy(buffer, bodyStart, bytes, 0, have);
                while (have < length)
                {
                    var count = await stream.ReadAsync(bytes.AsMemory(have, length - have), cancellationToken);
                    if (count == 0) return null;
                    have += count;
                }
                body = Encoding.UTF8.GetString(bytes);
            }

            return new HubRequest(parts[0].ToUpperInvariant(), path, query, attestation, body);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static string? QueryValue(string query, string name)
    {
        foreach (var pair in query.Split('&'))
        {
            var mark = pair.IndexOf('=');
            if (mark > 0 && pair[..mark] == name)
                return Uri.UnescapeDataString(pair[(mark + 1)..]);
        }
        return null;
    }

    private static int Find(byte[] buffer, int length)
    {
        for (var i = 0; i + 3 < length; i++)
            if (buffer[i] == '\r' && buffer[i + 1] == '\n' && buffer[i + 2] == '\r' && buffer[i + 3] == '\n')
                return i;
        return -1;
    }

    private static async Task WriteAsync(Stream stream, int status, string body, CancellationToken cancellationToken)
    {
        var payload = Encoding.UTF8.GetBytes(body);
        var head = Encoding.UTF8.GetBytes(
            $"HTTP/1.1 {status} {(status == 200 ? "OK" : "Error")}\r\n" +
            "Content-Type: application/json; charset=utf-8\r\n" +
            $"Content-Length: {payload.Length}\r\n" +
            "Connection: close\r\n\r\n");
        await stream.WriteAsync(head, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private sealed record HubRequest(string Method, string Path, string? Query, string? Attestation, string Body);
}

public static class HubHeaders
{
    public const string Attestation = "X-Cartex-Attestation";
}
