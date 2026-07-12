namespace Cartex.Mobile.Agent.Services;

public static class LeafletMap
{
    public static async Task<string> BuildHtmlAsync(string script)
    {
        var css = await ReadAssetAsync("map/leaflet.css");
        var js = await ReadAssetAsync("map/leaflet.js");
        return $$"""
<!doctype html>
<html>
<head>
<meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no" />
<style>{{css}}</style>
<style>
html,body,#map{margin:0;height:100%;}
.pin{width:26px;height:26px;background:#255D3A;border:3px solid #fff;border-radius:50% 50% 50% 0;transform:rotate(-45deg);box-shadow:0 2px 6px rgba(0,0,0,.4);}
.pin.red{background:#DC2626;}
.npin{width:28px;height:28px;background:#255D3A;color:#fff;border:2px solid #fff;border-radius:50%;display:flex;align-items:center;justify-content:center;font:700 13px sans-serif;box-shadow:0 2px 6px rgba(0,0,0,.4);}
.spin{width:16px;height:16px;background:#0EA5E9;border:3px solid #fff;border-radius:50%;box-shadow:0 2px 6px rgba(0,0,0,.4);}
</style>
</head>
<body>
<div id="map"></div>
<script>{{js}}</script>
<script>
var map = L.map('map', { zoomControl: false });
L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: '&copy; OpenStreetMap contributors' }).addTo(map);
{{script}}
</script>
</body>
</html>
""";
    }

    private static async Task<string> ReadAssetAsync(string path)
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync(path);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
