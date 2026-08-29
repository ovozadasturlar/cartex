using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using System.Collections.ObjectModel;
using Cartex.Shared.Models.Business;
using Cartex.Shared.Models.Common;
using Cartex.Shared.Models.Settings;
using Cartex.UI.Services;
using Refit;

namespace Cartex.UI.ViewModels;

public partial class BusinessSettingsViewModel : ViewModelBase, ILoadable
{
    private readonly IBusinessApi _api;
    private readonly IStorageApi _storageApi;
    private readonly IFilePickerService _filePicker;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private static readonly HttpClient _imageClient = new();

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _legalName = string.Empty;
    [ObservableProperty] private string _currency = "UZS";
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _telegram = string.Empty;
    [ObservableProperty] private string _website = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string? _logoImageKey;
    [ObservableProperty] private Bitmap? _logoPreview;
    [ObservableProperty] private string? _monochromeLogoImageKey;
    [ObservableProperty] private Bitmap? _monochromeLogoPreview;
    [ObservableProperty] private bool _isMonochromeLogoCustom;

    public ObservableCollection<string> Currencies { get; } = new(CurrencyCatalog.All);
    [ObservableProperty] private bool _qrLoginEnabled;
    [ObservableProperty] private decimal _qrRefreshSeconds = 120;
    [ObservableProperty] private bool _keyLoginEnabled = true;
    private bool _loginLoaded;

    private readonly ISettingsApi _settingsApi;
    private readonly AuthService _auth;

    public bool CanManageSecurity => _auth.HasPermission("settings.security");

    public BusinessSettingsViewModel(IBusinessApi api, IStorageApi storageApi, IFilePickerService filePicker, IToastService toast, IBusyService busy, ISettingsApi settingsApi, AuthService auth)
    {
        _settingsApi = settingsApi;
        _auth = auth;
        _api = api;
        _storageApi = storageApi;
        _filePicker = filePicker;
        _toast = toast;
        _busy = busy;
    }

    public async Task LoadAsync()
    {
        if (CanManageSecurity)
        try
        {
            var login = await _settingsApi.GetLoginMethodsAsync();
            QrLoginEnabled = login.QrEnabled;
            QrRefreshSeconds = login.QrRefreshSeconds;
            KeyLoginEnabled = login.KeyEnabled;
            _loginLoaded = true;
        }
        catch { }
        try
        {
            var b = await _api.GetAsync();
            Name = b.Name;
            LegalName = b.LegalName ?? string.Empty;
            if (!Currencies.Contains(b.Currency)) Currencies.Add(b.Currency);
            Currency = b.Currency;
            Phone = b.Phone ?? string.Empty;
            Address = b.Address ?? string.Empty;
            Telegram = b.Telegram ?? string.Empty;
            Website = b.Website ?? string.Empty;
            LogoImageKey = b.LogoImageKey;
            MonochromeLogoImageKey = b.MonochromeLogoImageKey;
            LogoPreview = await LoadBitmapAsync(b.LogoImageKey);
            MonochromeLogoPreview = await LoadBitmapAsync(b.MonochromeLogoImageKey ?? b.LogoImageKey);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private async Task<Bitmap?> LoadBitmapAsync(string? key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        try
        {
            var url = ImageUrl.Absolute((await _storageApi.GetUrlAsync(key)).Url);
            var bytes = await _imageClient.GetByteArrayAsync(url);
            return new Bitmap(new MemoryStream(bytes));
        }
        catch { return null; }
    }

    [RelayCommand]
    private async Task PickLogo()
    {
        try
        {
            var picked = await _filePicker.PickImageAsync();
            if (picked is null) return;
            using (_busy.Begin(L["loading"]))
            await using (picked.Content)
            {
                var result = await _storageApi.UploadLogoAsync(new StreamPart(picked.Content, picked.FileName, picked.ContentType));
                LogoImageKey = result.ColorKey;
                MonochromeLogoImageKey = result.MonochromeKey;
                IsMonochromeLogoCustom = false;
                LogoPreview = await LoadBitmapAsync(result.ColorKey);
                MonochromeLogoPreview = await LoadBitmapAsync(result.MonochromeKey);
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task PickMonochromeLogo()
    {
        try
        {
            var picked = await _filePicker.PickImageAsync();
            if (picked is null) return;
            using (_busy.Begin(L["loading"]))
            await using (picked.Content)
            {
                var result = await _storageApi.UploadAsync(
                    new StreamPart(picked.Content, picked.FileName, picked.ContentType));
                MonochromeLogoImageKey = result.Key;
                MonochromeLogoPreview = await LoadBitmapAsync(result.Key);
                IsMonochromeLogoCustom = true;
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)) { _toast.Warning(L["name"]); return; }
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                await _api.UpdateAsync(new UpdateBusinessRequest(
                    Name.Trim(),
                    string.IsNullOrWhiteSpace(LegalName) ? null : LegalName.Trim(),
                    Currency.Trim(),
                    string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim(),
                    string.IsNullOrWhiteSpace(Address) ? null : Address.Trim(),
                    LogoImageKey,
                    string.IsNullOrWhiteSpace(Telegram) ? null : Telegram.Trim(),
                    string.IsNullOrWhiteSpace(Website) ? null : Website.Trim(),
                    MonochromeLogoImageKey));
                if (_loginLoaded)
                    await _settingsApi.UpdateLoginMethodsAsync(new UpdateLoginMethodsRequest(
                        QrLoginEnabled, (int)Math.Clamp(QrRefreshSeconds, 30, 600), KeyLoginEnabled));
            }
            ServiceLocator.Resolve<ReferenceCache>().Invalidate(CacheKeys.Business);
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
