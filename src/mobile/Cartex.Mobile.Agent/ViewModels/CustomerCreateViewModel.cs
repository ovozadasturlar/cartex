using Cartex.ApiClient.Api;
using Cartex.Mobile.Agent.Services;
using Cartex.Shared.Models.Customers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.Mobile.Core;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class CustomerCreateViewModel(ICustomersApi customersApi, MobileAuthService auth, SyncService sync) : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _phone = "+998";
    [ObservableProperty] private string _address = "";
    [ObservableProperty] private string _creditLimit = "";
    [ObservableProperty] private double? _latitude;
    [ObservableProperty] private double? _longitude;
    [ObservableProperty] private string _locationText = "";
    [ObservableProperty] private bool _hasLocation;
    [ObservableProperty] private bool _locating;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isBusy;

    [RelayCommand]
    private async Task CaptureLocationAsync()
    {
        if (Locating) return;
        Locating = true;
        Error = null;
        try
        {
            var location = await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(15)))
                ?? await Geolocation.GetLastKnownLocationAsync();
            if (location is null)
            {
                Error = Loc.Instance["location_failed"];
                return;
            }
            SetLocation(location.Latitude, location.Longitude);
            await FillAddressAsync(location.Latitude, location.Longitude);
        }
        catch
        {
            Error = Loc.Instance["location_failed"];
        }
        finally
        {
            Locating = false;
        }
    }

    [RelayCommand]
    private Task PickOnMapAsync()
    {
        var query = new Dictionary<string, object> { ["picked"] = (Action<double, double>)OnMapPicked };
        if (HasLocation && Latitude is { } lat && Longitude is { } lng)
        {
            query["lat"] = lat;
            query["lng"] = lng;
        }
        return Shell.Current.GoToAsync("map-picker", query);
    }

    private void OnMapPicked(double lat, double lng)
    {
        SetLocation(lat, lng);
        _ = FillAddressAsync(lat, lng);
    }

    private void SetLocation(double lat, double lng)
    {
        Latitude = lat;
        Longitude = lng;
        LocationText = $"{lat:0.#####}, {lng:0.#####}";
        HasLocation = true;
    }

    private async Task FillAddressAsync(double lat, double lng)
    {
        if (!string.IsNullOrWhiteSpace(Address)) return;
        var address = await GetAddressAsync(lat, lng);
        if (!string.IsNullOrWhiteSpace(address) && string.IsNullOrWhiteSpace(Address))
            Address = address;
    }

    private static async Task<string?> GetAddressAsync(double lat, double lng)
    {
        try
        {
            if (!Android.Locations.Geocoder.IsPresent) return null;
            var geo = new Android.Locations.Geocoder(Platform.AppContext, Java.Util.Locale.ForLanguageTag("uz"));
            var place = (await geo.GetFromLocationAsync(lat, lng, 1))?.FirstOrDefault();
            if (place is null) return null;
            var parts = new[] { place.Thoroughfare, place.SubThoroughfare, place.SubLocality, place.Locality }
                .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct();
            return UzText.ToLatin(string.Join(", ", parts));
        }
        catch
        {
            return null;
        }
    }

    [RelayCommand]
    private void ClearLocation()
    {
        Latitude = null;
        Longitude = null;
        HasLocation = false;
        LocationText = "";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        Error = null;
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Phone) || Phone.Trim() == "+998")
        {
            Error = Loc.Instance["err_fill_all"];
            return;
        }

        IsBusy = true;
        try
        {
            decimal.TryParse(CreditLimit.Replace(" ", ""), out var limit);
            await customersApi.CreateAsync(new CreateCustomerRequest(
                Name.Trim(), Phone.Trim(), null, 0,
                Address: string.IsNullOrWhiteSpace(Address) ? null : Address.Trim(),
                CreditLimit: limit,
                AgentId: auth.UserId,
                Latitude: Latitude,
                Longitude: Longitude));
            await sync.SyncAsync();
            Ui.Toast(Loc.Instance["customer_created"]);
            await Shell.Current.GoToAsync("..");
        }
        catch (Refit.ApiException ex)
        {
            Error = SyncService.DescribeError(ex);
        }
        catch
        {
            Error = Loc.Instance["err_no_connection"];
        }
        finally
        {
            IsBusy = false;
        }
    }
}
