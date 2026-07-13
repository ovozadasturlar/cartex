namespace Cartex.ApiClient;

/// <summary>
/// Ochiq sahifaga tegishli o'qish so'rovlarining umri. Sahifa almashsa <see cref="CancelPending"/>
/// chaqiriladi va o'sha paytda ketayotgan sahifa GET so'rovlari bekor qilinadi — foydalanuvchi endi
/// ularning natijasini kutmaydi.
///
/// Faqat <see cref="BeginPageRequest"/> ichida boshlangan oqimdagi GET so'rovlari bog'lanadi:
/// fonda ketayotgan sinxronizatsiya, health-check va startdagi yuklashlar bekor qilinmaydi.
/// O'zgartiruvchi so'rovlar (POST/PUT/DELETE) hech qachon bekor qilinmaydi: sotuvni saqlash yoki
/// to'lovni yozish yarim yo'lda uzilishi ma'lumot yo'qotadi.
/// </summary>
public sealed class PageRequestScope
{
    private static readonly AsyncLocal<bool> PageScoped = new();

    private CancellationTokenSource _cts = new();

    public CancellationToken Token => _cts.Token;

    public bool IsPageScoped => PageScoped.Value;

    /// <summary>Sahifa yuklanishini boshlaydi: shu oqimdagi GET so'rovlari sahifa umriga bog'lanadi.</summary>
    public IDisposable BeginPageRequest()
    {
        PageScoped.Value = true;
        return new Reset();
    }

    public void CancelPending()
    {
        var previous = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        previous.Cancel();
        previous.Dispose();
    }

    private sealed class Reset : IDisposable
    {
        public void Dispose() => PageScoped.Value = false;
    }
}

public sealed class PageRequestScopeHandler(PageRequestScope scope) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Get || !scope.IsPageScoped)
            return await base.SendAsync(request, cancellationToken);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, scope.Token);
        return await base.SendAsync(request, linked.Token);
    }
}
