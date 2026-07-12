using System.Globalization;

namespace Cartex.Mobile.Agent.Views;

public partial class MapPickerPage : ContentPage, IQueryAttributable
{
    private double? _lat;
    private double? _lng;
    private Action<double, double>? _picked;
    private bool _loaded;

    public MapPickerPage()
    {
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("lat", out var lat)) _lat = (double)lat;
        if (query.TryGetValue("lng", out var lng)) _lng = (double)lng;
        if (query.TryGetValue("picked", out var picked)) _picked = (Action<double, double>)picked;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;
        _loaded = true;
        Web.Source = new HtmlWebViewSource { Html = await BuildHtmlAsync() };
    }

    private Task<string> BuildHtmlAsync()
    {
        var lat = (_lat ?? 41.3111).ToString(CultureInfo.InvariantCulture);
        var lng = (_lng ?? 69.2797).ToString(CultureInfo.InvariantCulture);
        var zoom = _lat is null ? "12" : "16";
        return Services.LeafletMap.BuildHtmlAsync($$"""
map.setView([{{lat}}, {{lng}}], {{zoom}});
var icon = L.divIcon({ className: '', html: '<div class="pin"></div>', iconSize: [26, 26], iconAnchor: [13, 26] });
var marker = L.marker([{{lat}}, {{lng}}], { icon: icon, draggable: true }).addTo(map);
map.on('click', function (e) { marker.setLatLng(e.latlng); });
function pos() { var p = marker.getLatLng(); return p.lat + ',' + p.lng; }
""");
    }

    private async void OnConfirm(object? sender, EventArgs e)
    {
        var result = (await Web.EvaluateJavaScriptAsync("pos()"))?.Trim('"', '\\');
        var parts = result?.Split(',');
        if (parts?.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lng))
            _picked?.Invoke(lat, lng);
        await Shell.Current.GoToAsync("..");
    }
}
