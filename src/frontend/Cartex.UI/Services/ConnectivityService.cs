using System;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cartex.UI.Services;

public partial class ConnectivityService : ObservableObject
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(4) };
    private DispatcherTimer? _timer;

    [ObservableProperty] private bool _isOnline = true;

    public void Start()
    {
        if (_timer is not null) return;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _timer.Tick += async (_, _) => await CheckAsync();
        _timer.Start();
        _ = CheckAsync();
    }

    private async Task CheckAsync()
    {
        try
        {
            using var response = await _http.GetAsync(SettingsService.Instance.ApiBaseUrl.TrimEnd('/') + "/health");
            IsOnline = response.IsSuccessStatusCode;
        }
        catch { IsOnline = false; }
    }
}
