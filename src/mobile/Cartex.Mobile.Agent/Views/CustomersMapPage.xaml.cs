using System.Net;
using System.Text.Json;
using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Services;
using Cartex.Mobile.Core;

namespace Cartex.Mobile.Agent.Views;

public partial class CustomersMapPage : ContentPage
{
    private readonly AgentDb _db;
    private bool _loaded;

    public CustomersMapPage(AgentDb db)
    {
        InitializeComponent();
        _db = db;
        Web.Navigating += OnNavigating;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;
        _loaded = true;
        var currency = await _db.GetMetaAsync("base_currency") ?? "";
        var points = (await _db.GetCustomersAsync())
            .Where(c => c is { Latitude: not null, Longitude: not null })
            .Select(c => new
            {
                lat = c.Latitude,
                lng = c.Longitude,
                id = c.Id,
                name = WebUtility.HtmlEncode(c.FullName),
                debt = c.DebtBalance > 0 ? $"{Loc.Instance["debt"]}: {c.DebtBalance:N0} {currency}" : ""
            })
            .ToList();
        if (points.Count == 0)
        {
            Web.IsVisible = false;
            Empty.IsVisible = true;
            return;
        }
        var json = JsonSerializer.Serialize(points);
        Web.Source = new HtmlWebViewSource
        {
            Html = await LeafletMap.BuildHtmlAsync($$"""
var pts = {{json}};
var group = [];
pts.forEach(function (p) {
  var icon = L.divIcon({ className: '', html: '<div class="pin' + (p.debt ? ' red' : '') + '"></div>', iconSize: [26, 26], iconAnchor: [13, 26] });
  var m = L.marker([p.lat, p.lng], { icon: icon }).addTo(map);
  m.bindPopup('<b>' + p.name + '</b>' + (p.debt ? '<br>' + p.debt : '') + '<br><a href="app://customer/' + p.id + '">{{Loc.Instance["open"]}}</a>');
  group.push(m);
});
map.fitBounds(L.featureGroup(group).getBounds().pad(0.2));
""")
        };
    }

    private async void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (!e.Url.StartsWith("app://customer/")) return;
        e.Cancel = true;
        if (long.TryParse(e.Url["app://customer/".Length..], out var id))
            await Shell.Current.GoToAsync("customer", new Dictionary<string, object> { ["customerId"] = id });
    }
}
