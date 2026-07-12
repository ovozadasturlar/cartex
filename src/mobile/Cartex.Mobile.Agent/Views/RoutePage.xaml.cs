using System.Globalization;
using System.Net;
using System.Text.Json;
using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Services;
using Cartex.Mobile.Core;

namespace Cartex.Mobile.Agent.Views;

public sealed record RouteStop(int Index, string Name, string SubText, string DistText, double Lat, double Lng);

public partial class RoutePage : ContentPage
{
    private readonly AgentDb _db;
    private bool _loaded;

    public RoutePage(AgentDb db)
    {
        InitializeComponent();
        _db = db;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;
        _loaded = true;

        var orders = (await _db.GetOrdersAsync()).Where(o => o.Status != "delivered" && o.CustomerId is not null).ToList();
        var customers = (await _db.GetCustomersAsync()).ToDictionary(c => c.Id);
        var currency = await _db.GetMetaAsync("base_currency") ?? "";

        var stops = new List<(LocalCustomer Customer, int Count, decimal Total)>();
        var skipped = 0;
        foreach (var g in orders.GroupBy(o => o.CustomerId!.Value))
        {
            if (customers.TryGetValue(g.Key, out var c) && c is { Latitude: not null, Longitude: not null })
                stops.Add((c, g.Count(), g.Sum(o => o.Total)));
            else
                skipped++;
        }
        if (stops.Count == 0)
        {
            Web.IsVisible = false;
            Empty.IsVisible = true;
            return;
        }

        Location? me = null;
        try
        {
            me = await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(8)))
                ?? await Geolocation.GetLastKnownLocationAsync();
        }
        catch { }

        var ordered = Optimize(stops, me?.Latitude ?? stops[0].Customer.Latitude!.Value, me?.Longitude ?? stops[0].Customer.Longitude!.Value);

        var rows = new List<RouteStop>();
        double lat = me?.Latitude ?? ordered[0].Customer.Latitude!.Value, lng = me?.Longitude ?? ordered[0].Customer.Longitude!.Value;
        for (var i = 0; i < ordered.Count; i++)
        {
            var (c, count, total) = ordered[i];
            var dist = me is null && i == 0 ? 0 : Dist(lat, lng, c.Latitude!.Value, c.Longitude!.Value);
            rows.Add(new RouteStop(i + 1, c.FullName, $"{count} {Loc.Instance["orders_short"]} • {total:N0} {currency}",
                dist > 0 ? $"{dist:0.#} km" : "", c.Latitude!.Value, c.Longitude!.Value));
            lat = c.Latitude!.Value;
            lng = c.Longitude!.Value;
        }

        BindableLayout.SetItemsSource(StopsHost, rows);
        ListCard.IsVisible = true;
        if (skipped > 0)
        {
            SkippedNote.Text = string.Format(Loc.Instance["route_skipped_fmt"], skipped);
            SkippedNote.IsVisible = true;
        }

        var pts = JsonSerializer.Serialize(rows.Select(r => new { r.Lat, r.Lng, I = r.Index, Name = WebUtility.HtmlEncode(r.Name) }));
        var startJs = me is null
            ? ""
            : $"coords.push([{me.Latitude.ToString(CultureInfo.InvariantCulture)}, {me.Longitude.ToString(CultureInfo.InvariantCulture)}]);" +
              $"L.marker(coords[0], {{ icon: L.divIcon({{ className: '', html: '<div class=\"spin\"></div>', iconSize: [16, 16], iconAnchor: [8, 8] }}) }}).addTo(map);";
        Web.Source = new HtmlWebViewSource
        {
            Html = await LeafletMap.BuildHtmlAsync($$"""
var pts = {{pts}};
var coords = [];
{{startJs}}
pts.forEach(function (p) {
  coords.push([p.Lat, p.Lng]);
  var m = L.marker([p.Lat, p.Lng], { icon: L.divIcon({ className: '', html: '<div class="npin">' + p.I + '</div>', iconSize: [28, 28], iconAnchor: [14, 14] }) }).addTo(map);
  m.bindPopup('<b>' + p.I + '. ' + p.Name + '</b>');
});
L.polyline(coords, { color: '#255D3A', weight: 3, opacity: 0.7, dashArray: '6 6' }).addTo(map);
map.fitBounds(L.latLngBounds(coords).pad(0.2));
""")
        };
    }

    private async void OnStopTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not RouteStop stop) return;
        var lat = stop.Lat.ToString(CultureInfo.InvariantCulture);
        var lng = stop.Lng.ToString(CultureInfo.InvariantCulture);
        if (!await Launcher.TryOpenAsync($"google.navigation:q={lat},{lng}"))
            await Launcher.OpenAsync(new Uri($"geo:0,0?q={lat},{lng}({Uri.EscapeDataString(stop.Name)})"));
    }

    private static List<(LocalCustomer Customer, int Count, decimal Total)> Optimize(
        List<(LocalCustomer Customer, int Count, decimal Total)> stops, double startLat, double startLng)
    {
        var route = new List<(LocalCustomer Customer, int Count, decimal Total)>();
        var remaining = new List<(LocalCustomer Customer, int Count, decimal Total)>(stops);
        double lat = startLat, lng = startLng;
        while (remaining.Count > 0)
        {
            var next = remaining.MinBy(s => Dist(lat, lng, s.Customer.Latitude!.Value, s.Customer.Longitude!.Value));
            route.Add(next);
            remaining.Remove(next);
            lat = next.Customer.Latitude!.Value;
            lng = next.Customer.Longitude!.Value;
        }

        double D(int a, int b)
        {
            var (la, lo) = a < 0 ? (startLat, startLng) : (route[a].Customer.Latitude!.Value, route[a].Customer.Longitude!.Value);
            return Dist(la, lo, route[b].Customer.Latitude!.Value, route[b].Customer.Longitude!.Value);
        }

        for (var improved = true; improved;)
        {
            improved = false;
            for (var i = 0; i < route.Count - 1; i++)
                for (var j = i + 1; j < route.Count; j++)
                {
                    var delta = D(i - 1, j) - D(i - 1, i);
                    if (j < route.Count - 1) delta += D(i, j + 1) - D(j, j + 1);
                    if (delta < -1e-9)
                    {
                        route.Reverse(i, j - i + 1);
                        improved = true;
                    }
                }
        }
        return route;
    }

    private static double Dist(double lat1, double lng1, double lat2, double lng2)
    {
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLng = (lng2 - lng1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 6371 * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
