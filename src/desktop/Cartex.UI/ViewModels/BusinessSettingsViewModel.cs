using System.IO;
using System.Net.Http;
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
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string? _logoImageKey;
    [ObservableProperty] private Bitmap? _logoPreview;

    public ObservableCollection<string> Currencies { get; } = new(CurrencyCatalog.All);
    public ObservableCollection<string> ShiftPolicies { get; } = [];
    private static readonly string[] ShiftPolicyCodes = ["Off", "CashOnly", "AllSales"];

    [ObservableProperty] private int _shiftPolicyIndex = 1;
    [ObservableProperty] private decimal _maxDiscountPercent;
    [ObservableProperty] private decimal _defaultMinStock;
    [ObservableProperty] private decimal _staleRateDays = 3;
    [ObservableProperty] private bool _qrLoginEnabled;
    [ObservableProperty] private decimal _qrRefreshSeconds = 120;
    [ObservableProperty] private bool _keyLoginEnabled = true;
    private bool _policyLoaded;
    private bool _loginLoaded;

    private readonly ISettingsApi _settingsApi;

    public BusinessSettingsViewModel(IBusinessApi api, IStorageApi storageApi, IFilePickerService filePicker, IToastService toast, IBusyService busy, ISettingsApi settingsApi)
    {
        _settingsApi = settingsApi;
        _api = api;
        _storageApi = storageApi;
        _filePicker = filePicker;
        _toast = toast;
        _busy = busy;
    }

    public async Task LoadAsync()
    {
        ShiftPolicies.Clear();
        foreach (var code in ShiftPolicyCodes) ShiftPolicies.Add(L[$"shift_policy_{code.ToLowerInvariant()}"]);
        try
        {
            var policy = await _settingsApi.GetSalesPolicyAsync();
            ShiftPolicyIndex = Math.Max(0, Array.IndexOf(ShiftPolicyCodes, policy.ShiftPolicy));
            MaxDiscountPercent = policy.MaxDiscountPercent;
            DefaultMinStock = policy.DefaultMinStock;
            StaleRateDays = policy.StaleRateDays;
            _policyLoaded = true;
        }
        catch { }
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
            LogoImageKey = b.LogoImageKey;
            LogoPreview = await LoadBitmapAsync(b.LogoImageKey);
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
                var result = await _storageApi.UploadAsync(new StreamPart(picked.Content, picked.FileName, picked.ContentType));
                LogoImageKey = result.Key;
                LogoPreview = await LoadBitmapAsync(result.Key);
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
                    LogoImageKey));
                if (_policyLoaded)
                    await _settingsApi.UpdateSalesPolicyAsync(new UpdateSalesPolicyRequest(
                        ShiftPolicyCodes[Math.Clamp(ShiftPolicyIndex, 0, 2)], MaxDiscountPercent, DefaultMinStock, (int)StaleRateDays));
                if (_loginLoaded)
                    await _settingsApi.UpdateLoginMethodsAsync(new UpdateLoginMethodsRequest(
                        QrLoginEnabled, (int)Math.Clamp(QrRefreshSeconds, 30, 600), KeyLoginEnabled));
            }
            ServiceLocator.Resolve<ReferenceCache>().Invalidate(CacheKeys.Business, CacheKeys.SalesPolicy);
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
