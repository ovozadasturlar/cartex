using System.Text;

namespace Cartex.Mobile.Core;

public static class QrActions
{
    private const string ServerPrefix = "cartexsrv:";

    public static bool TryServer(string value, out string url)
    {
        url = "";
        if (!value.StartsWith(ServerPrefix, StringComparison.OrdinalIgnoreCase)) return false;
        var candidate = value[ServerPrefix.Length..].Trim();
        if (!IsHttpUrl(candidate)) return false;
        url = candidate;
        return true;
    }

    public static bool TryServerOrUrl(string value, out string url)
    {
        if (TryServer(value, out url)) return true;
        if (!IsHttpUrl(value)) return false;
        url = value;
        return true;
    }

    // Userinfo'li manzil ("http://192.168.1.5@evil.com") dialogda ishonchli host bo'lib
    // ko'rinadi, lekin so'rov evil.com'ga ketadi — shuning uchun umuman qabul qilinmaydi.
    public static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && string.IsNullOrEmpty(uri.UserInfo);

    public static bool IsSameServer(string left, string right) =>
        Uri.TryCreate(left, UriKind.Absolute, out var a)
        && Uri.TryCreate(right, UriKind.Absolute, out var b)
        && Uri.Compare(a, b, UriComponents.SchemeAndServer, UriFormat.UriEscaped, StringComparison.OrdinalIgnoreCase) == 0;

    // Faqat anonim endpointlar so'raladi: skanerlangan host hali ishonchsiz, unga
    // sessiya tokeni yuborilmasligi shart.
    public static async Task<bool> ProbeServerAsync(string url)
    {
        var root = url.TrimEnd('/');
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            (await http.GetAsync($"{root}/health")).EnsureSuccessStatusCode();
            var response = await http.GetAsync($"{root}/api/auth/login-methods");
            response.EnsureSuccessStatusCode();
            var media = response.Content.Headers.ContentType?.MediaType;
            return media is not null && media.Contains("json", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsWifi(string value) =>
        value.StartsWith("WIFI:", StringComparison.OrdinalIgnoreCase);

    public static async Task OpenLinkAsync(string url)
    {
        var page = Shell.Current?.CurrentPage;
        if (page is null || !await page.DisplayAlertAsync(
                Loc.Instance["open_link_confirm"], url, Loc.Instance["yes"], Loc.Instance["no"]))
            return;
        await Launcher.Default.OpenAsync(url);
    }

    public static async Task HandleWifiAsync(string value)
    {
        var page = Shell.Current?.CurrentPage;
        if (page is null) return;
        var (ssid, password) = ParseWifi(value);
        var title = string.IsNullOrEmpty(ssid) ? Loc.Instance["wifi_network"] : ssid;
        if (string.IsNullOrEmpty(password))
        {
            if (await page.DisplayAlertAsync(title, Loc.Instance["wifi_open_settings_plain"], Loc.Instance["yes"], Loc.Instance["no"]))
                OpenWifiSettings();
            return;
        }
        if (!await page.DisplayAlertAsync(title, Loc.Instance["wifi_open_settings"], Loc.Instance["yes"], Loc.Instance["no"]))
            return;
        await Clipboard.Default.SetTextAsync(password);
        Ui.Toast(Loc.Instance["wifi_password_copied"]);
        OpenWifiSettings();
    }

    private static (string Ssid, string Password) ParseWifi(string value)
    {
        string ssid = "", password = "";
        foreach (var field in SplitFields(value["WIFI:".Length..]))
        {
            if (field.StartsWith("S:", StringComparison.OrdinalIgnoreCase)) ssid = Unescape(field[2..]);
            else if (field.StartsWith("P:", StringComparison.OrdinalIgnoreCase)) password = Unescape(field[2..]);
        }
        return (ssid, password);
    }

    private static List<string> SplitFields(string body)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        for (var i = 0; i < body.Length; i++)
        {
            var c = body[i];
            if (c == '\\' && i + 1 < body.Length)
                current.Append(c).Append(body[++i]);
            else if (c == ';')
            {
                if (current.Length > 0) fields.Add(current.ToString());
                current.Clear();
            }
            else
                current.Append(c);
        }
        if (current.Length > 0) fields.Add(current.ToString());
        return fields;
    }

    private static string Unescape(string text)
    {
        var result = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length) i++;
            result.Append(text[i]);
        }
        return result.ToString();
    }

    private static void OpenWifiSettings()
    {
#if ANDROID
        try
        {
            var intent = new Android.Content.Intent(Android.Provider.Settings.ActionWifiSettings);
            intent.SetFlags(Android.Content.ActivityFlags.NewTask);
            Android.App.Application.Context.StartActivity(intent);
        }
        catch
        {
        }
#endif
    }
}
