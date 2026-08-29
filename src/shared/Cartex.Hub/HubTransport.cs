using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Cartex.Hub;

/// HUB-04: yo'ldosh tomonining kanali. Sertifikat zanjiri (CA) tekshirilmaydi — ishonch bulut
/// imzolagan guvohnomadagi ochiq kalitdan keladi, shuning uchun kanal aynan shu kalitga bog'lanadi.
public static class HubTransport
{
    /// Kutilgan HUB ochiq kaliti: qo'yilgan bo'lsa qo'l siqishning o'zida majburlanadi.
    public static readonly HttpRequestOptionsKey<string> ExpectedKey = new("cartex-hub-expected");

    /// Qo'l siqishda ko'rilgan tomonning ochiq kaliti — birinchi salomda guvohnoma bilan solishtiriladi.
    public static readonly HttpRequestOptionsKey<string> PeerKey = new("cartex-hub-peer");

    /// Qo'l siqish `ConnectCallback` ichida qilinadi: faqat shu yerda ulanishni boshlagan so'rov
    /// ma'lum bo'ladi, ya'ni tekshiruv aynan o'sha so'rovning kutilgan kalitiga bog'lanadi.
    public static HttpClient Create(HubIdentityKey identity, TimeSpan timeout) =>
        new(new SocketsHttpHandler
        {
            // Ulanish qayta ishlatilmaydi: shundagina har so'rov o'z qo'l siqishiga ega bo'ladi va
            // kalit tekshiruvi avval ochilgan ulanishga ishonib qolmaydi.
            PooledConnectionLifetime = TimeSpan.Zero,
            ConnectCallback = (context, cancellationToken) => ConnectAsync(identity, context, cancellationToken)
        })
        {
            Timeout = timeout
        };

    private static async ValueTask<Stream> ConnectAsync(
        HubIdentityKey identity, SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        SslStream? stream = null;
        try
        {
            await socket.ConnectAsync(context.DnsEndPoint, cancellationToken);
            stream = new SslStream(new NetworkStream(socket, ownsSocket: true));
            await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = context.DnsEndPoint.Host,
                ClientCertificates = new X509CertificateCollection { identity.Certificate },
                // Server ishonchli CA ro'yxatini bermaydi; tanlovni majburlamasak Windows
                // klient sertifikatini umuman yubormasdi.
                LocalCertificateSelectionCallback = (_, _, _, _, _) => identity.Certificate,
                RemoteCertificateValidationCallback = (_, certificate, _, _) =>
                    Validate(context.InitialRequestMessage, certificate),
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }, cancellationToken);
            return stream;
        }
        catch
        {
            if (stream is not null) await stream.DisposeAsync();
            else socket.Dispose();
            throw;
        }
    }

    private static bool Validate(HttpRequestMessage request, X509Certificate? certificate)
    {
        if (certificate is not X509Certificate2 peer || HubIdentityKey.PublicKeyOf(peer) is not { } observed)
            return false;

        if (request.Options.TryGetValue(ExpectedKey, out var expected))
            return HubIdentityKey.SameKey(observed, expected);

        // Hali HUB kaliti noma'lum (birinchi salom): kalit yozib olinadi va javob guvohnoma
        // bilan solishtirilgandan keyin qabul qilinadi.
        request.Options.Set(PeerKey, observed);
        return true;
    }
}
