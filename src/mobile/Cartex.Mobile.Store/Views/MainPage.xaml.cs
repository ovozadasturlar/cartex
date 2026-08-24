using Cartex.Mobile.Core;

namespace Cartex.Mobile.Store.Views;

public partial class MainPage : ContentPage
{
    private const int SectionCount = 5;

    private readonly IServiceProvider _services;
    private readonly AccessState _access;
    private readonly View?[] _sections = new View?[SectionCount];
    private readonly bool[] _available = new bool[SectionCount];
    private int _current = -1;

    public static MainPage? Current { get; private set; }

    public MainPage(IServiceProvider services, AccessState access)
    {
        InitializeComponent();
        _services = services;
        _access = access;
        _access.Changed += OnAccessChanged;
        Dispatcher.StartTimer(TimeSpan.FromMinutes(1), () =>
        {
            RefreshAccessUi();
            return true;
        });
        Bar.Selected = index => Show(index);
        Current = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshAccessUi();
        if (!_access.IsLoaded) return;
        if (_current < 0)
        {
            Show(0);
            WarmSections();
        }
        else
        {
            Active?.Appear();
        }
    }

    private async void OnAccessChanged() => await Dispatcher.DispatchAsync(() =>
    {
        RefreshAccessUi();
        if (_access.IsLoaded && IsVisible && _current < 0)
        {
            Show(0);
            WarmSections();
        }
    });

    private void RefreshAccessUi()
    {
        AccessLoading.IsVisible = _access.State == AccessLoadState.Loading;
        AccessFailure.IsVisible = _access.State == AccessLoadState.Failed;
        AccessFailureText.Text = Loc.Instance[_access.FailureKind switch
        {
            AccessFailureKind.Network => "access_network_error",
            AccessFailureKind.Server => "access_server_error",
            AccessFailureKind.SessionInvalid => "access_session_error",
            _ => "access_unknown_error"
        }];
        StaleBanner.IsVisible = _access.IsStale;
        StaleText.Text = string.Format(Loc.Instance["access_stale_fmt"],
            _access.LastSuccessfulRefresh?.ToLocalTime().ToString("HH:mm") ?? "—");
        _available[0] = _access.CanViewHome;
        _available[1] = _access.CanViewTrade;
        _available[2] = _access.CanUseScanner;
        _available[3] = _access.CanViewCustomers;
        _available[4] = true;
        Bar.SetAvailable(_available);
        if (_access.IsLoaded && (_current < 0 || !_available[_current]))
            Show(Array.FindIndex(_available, value => value));
    }

    private async void RetryAccess_Clicked(object? sender, EventArgs e) =>
        await _access.RefreshAsync();

    // Bo'lim birinchi marta ochilganda XAML yoyilishi animatsiya bilan bir vaqtda tushadi
    // va kadr tashlanadi. Uy ekrani chiqqach, qolganlari bo'sh kadrlarda birma-bir
    // tayyorlanadi — shundan keyin har o'tish bir xil silliq bo'ladi.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Meziantou.Analyzer", "MA0045",
        Justification = "Kadr-ba-kadr isitish ataylab fire-and-forget: DispatchAsync kutilsa isitish bir kadrga yig'ilib qoladi.")]
    private void WarmSections()
    {
        var next = 1;
        void Step()
        {
            while (next < SectionCount && !_available[next]) next++;
            if (next >= SectionCount) return;
            var index = next++;
            var section = _sections[index] ??= Create(index);
            if (section.Parent is null)
            {
                section.IsVisible = false;
                Host.Add(section);
            }
            Dispatcher.Dispatch(Step);
        }
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(600), Step);
    }

    protected override void OnDisappearing()
    {
        Active?.Disappear();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed() =>
        Active?.HandleBack() == true || base.OnBackButtonPressed();

    private ISectionView? Active => _current >= 0 ? _sections[_current] as ISectionView : null;

    // Bo'lim ichidagi overlay ochilganda panel yashiriladi; panel bitta bo'lgani uchun
    // buni bo'limning o'zi so'raydi.
    public bool BarVisible
    {
        get => Bar.IsVisible;
        set => Bar.IsVisible = value;
    }

    public void Show(int index, string? argument = null)
    {
        if (index < 0 || index >= SectionCount || !_available[index]) return;
        if (index == _current)
        {
            if (argument is not null && _sections[index] is IArgumentAware aware) aware.Apply(argument);
            return;
        }

        Active?.Disappear();
        var section = _sections[index] ??= Create(index);
        if (section.Parent is null) Host.Add(section);
        for (var i = 0; i < SectionCount; i++)
            if (_sections[i] is { } view) view.IsVisible = i == index;

        var previous = _current;
        _current = index;
        BarVisible = true;
        if (argument is not null && section is IArgumentAware ready) ready.Apply(argument);
        (section as ISectionView)?.Appear();
        Bar.Select(index, animateFrom: previous);
    }

    private View Create(int index) => (View)_services.GetRequiredService(index switch
    {
        0 => typeof(HomeView),
        1 => typeof(TradeView),
        2 => typeof(ScanView),
        3 => typeof(CustomersView),
        _ => typeof(ProfileView),
    });
}

// Bo'limga navigatsiya bilan qiymat uzatish (masalan savdolar ro'yxatini ochish).
public interface IArgumentAware
{
    void Apply(string argument);
}
