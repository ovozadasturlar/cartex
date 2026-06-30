using System.IO;
using System.Net.Http;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Business;
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

    public BusinessSettingsViewModel(IBusinessApi api, IStorageApi storageApi, IFilePickerService filePicker, IToastService toast, IBusyService busy)
    {
        _api = api;
        _storageApi = storageApi;
        _filePicker = filePicker;
        _toast = toast;
        _busy = busy;
    }

    public async Task LoadAsync()
    {
        try
        {
            var b = await _api.GetAsync();
            Name = b.Name;
            LegalName = b.LegalName ?? string.Empty;
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
            var url = (await _storageApi.GetUrlAsync(key)).Url;
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
                await _api.UpdateAsync(new UpdateBusinessRequest(
                    Name.Trim(),
                    string.IsNullOrWhiteSpace(LegalName) ? null : LegalName.Trim(),
                    Currency.Trim(),
                    string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim(),
                    string.IsNullOrWhiteSpace(Address) ? null : Address.Trim(),
                    LogoImageKey));
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
