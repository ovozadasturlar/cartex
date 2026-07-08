using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Sales;
using Cartex.UI.Services;

namespace Cartex.UI.Controls;

public partial class ReceiptCard : UserControl
{
    private static string? _logoUrl;
    private static bool _logoResolved;

    public static readonly StyledProperty<string?> LogoUrlProperty =
        AvaloniaProperty.Register<ReceiptCard, string?>(nameof(LogoUrl));

    public static readonly StyledProperty<Bitmap?> QrCodeProperty =
        AvaloniaProperty.Register<ReceiptCard, Bitmap?>(nameof(QrCode));

    public string? LogoUrl { get => GetValue(LogoUrlProperty); set => SetValue(LogoUrlProperty, value); }
    public Bitmap? QrCode { get => GetValue(QrCodeProperty); set => SetValue(QrCodeProperty, value); }

    public ReceiptCard()
    {
        InitializeComponent();
        DataContextChanged += OnReceiptChanged;
    }

    private async void OnReceiptChanged(object? sender, EventArgs e)
    {
        if (DataContext is not ReceiptDto receipt)
        {
            QrCode = null;
            return;
        }

        QrCode = QrService.Generate($"{SettingsService.Instance.ApiBaseUrl}/r/{receipt.ReceiptToken}");
        LogoUrl = await ResolveLogoAsync();
    }

    private static async Task<string?> ResolveLogoAsync()
    {
        if (_logoResolved) return _logoUrl;
        _logoResolved = true;
        try
        {
            var business = await ServiceLocator.Resolve<IBusinessApi>().GetAsync();
            if (!string.IsNullOrEmpty(business.LogoImageKey))
                _logoUrl = (await ServiceLocator.Resolve<IStorageApi>().GetUrlAsync(business.LogoImageKey)).Url;
        }
        catch { }
        return _logoUrl;
    }
}
