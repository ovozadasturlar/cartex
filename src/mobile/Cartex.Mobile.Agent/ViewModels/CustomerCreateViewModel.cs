using Cartex.ApiClient.Api;
using Cartex.Mobile.Agent.Services;
using Cartex.Shared.Models.Customers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
            Latitude = location.Latitude;
            Longitude = location.Longitude;
            LocationText = $"{location.Latitude:0.#####}, {location.Longitude:0.#####}";
            HasLocation = true;

            if (string.IsNullOrWhiteSpace(Address))
            {
                try
                {
                    var place = (await Geocoding.GetPlacemarksAsync(location.Latitude, location.Longitude)).FirstOrDefault();
                    if (place is not null)
                    {
                        var parts = new[] { place.Thoroughfare, place.SubThoroughfare, place.SubLocality, place.Locality }
                            .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct();
                        Address = string.Join(", ", parts);
                    }
                }
                catch { }
            }
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
