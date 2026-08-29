using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Catalog;
using Cartex.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace Cartex.UI.ViewModels;

public partial class CatalogSettingsViewModel : ViewModelBase, ILoadable
{
    private static readonly CatalogSourceMode[] Modes = [CatalogSourceMode.Online, CatalogSourceMode.File, CatalogSourceMode.Off];

    private readonly ISettingsApi _settingsApi;
    private readonly IFilePickerService _filePicker;
    private readonly IDialogService _dialog;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;

    public ObservableCollection<string> ModeNames { get; } = [];

    [ObservableProperty] private int _modeIndex;
    [ObservableProperty] private string _endpointBaseUrl = string.Empty;
    [ObservableProperty] private string _imageBaseUrl = string.Empty;
    [ObservableProperty] private CatalogPackDto? _pack;
    [ObservableProperty] private string? _lastError;

    public bool IsFileMode => ModeIndex == 1;
    public bool IsOnlineMode => ModeIndex == 0;
    public bool IsOffMode => ModeIndex == 2;
    public bool HasPack => Pack is not null;
    public string PackUploadedText => Pack is null ? L["never"] : Pack.UploadedAt.ToLocalTime().ToString("g");
    public string UploadButtonText => HasPack ? L["catalog_pack_replace"] : L["catalog_pack_upload"];
    public string ModeHint => ModeIndex switch
    {
        0 => L["catalog_mode_online_hint"],
        1 => L["catalog_mode_file_hint"],
        _ => L["catalog_mode_off_hint"]
    };

    public CatalogSettingsViewModel(ISettingsApi settingsApi, IFilePickerService filePicker, IDialogService dialog,
        IToastService toast, IBusyService busy)
    {
        _settingsApi = settingsApi;
        _filePicker = filePicker;
        _dialog = dialog;
        _toast = toast;
        _busy = busy;
        ModeNames.Add(L["catalog_mode_online"]);
        ModeNames.Add(L["catalog_mode_file"]);
        ModeNames.Add(L["catalog_mode_off"]);
    }

    partial void OnModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsFileMode));
        OnPropertyChanged(nameof(IsOnlineMode));
        OnPropertyChanged(nameof(IsOffMode));
        OnPropertyChanged(nameof(ModeHint));
    }

    partial void OnPackChanged(CatalogPackDto? value)
    {
        OnPropertyChanged(nameof(HasPack));
        OnPropertyChanged(nameof(PackUploadedText));
        OnPropertyChanged(nameof(UploadButtonText));
    }

    public async Task LoadAsync()
    {
        try
        {
            CatalogSettingsDto settings;
            using (_busy.Begin(L["loading"])) settings = await _settingsApi.GetCatalogAsync();
            ModeIndex = Math.Max(0, Array.IndexOf(Modes, settings.Mode));
            EndpointBaseUrl = settings.EndpointBaseUrl;
            ImageBaseUrl = settings.ImageBaseUrl;
            Pack = settings.Pack;
            LastError = settings.LastError;
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
                await _settingsApi.UpdateCatalogAsync(new CatalogSettingsDto
                {
                    Mode = Modes[ModeIndex],
                    EndpointBaseUrl = EndpointBaseUrl.Trim(),
                    ImageBaseUrl = ImageBaseUrl.Trim()
                });
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task UploadPackAsync()
    {
        var pack = await _filePicker.PickCatalogPackAsync();
        if (pack is null) return;
        var manifest = await _filePicker.PickJsonAsync();
        if (manifest is null)
        {
            await pack.Content.DisposeAsync();
            return;
        }

        try
        {
            using (_busy.Begin(L["loading"]))
                Pack = await _settingsApi.UploadCatalogPackAsync(
                    new StreamPart(pack.Content, pack.FileName, pack.ContentType),
                    new StreamPart(manifest.Content, manifest.FileName, manifest.ContentType));
            LastError = null;
            _toast.Success(L["catalog_pack_uploaded"]);
        }
        catch (Exception ex)
        {
            LastError = ApiErrors.Describe(ex);
            _toast.Error(LastError);
        }
        finally
        {
            await pack.Content.DisposeAsync();
            await manifest.Content.DisposeAsync();
        }
    }

    [RelayCommand]
    private async Task DeletePackAsync()
    {
        if (!await _dialog.ConfirmAsync(L["catalog_pack_delete_confirm"], L["catalog_pack"])) return;
        try
        {
            using (_busy.Begin(L["loading"])) await _settingsApi.DeleteCatalogPackAsync();
            Pack = null;
            LastError = null;
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
