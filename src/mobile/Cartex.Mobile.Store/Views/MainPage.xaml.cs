namespace Cartex.Mobile.Store.Views;

public partial class MainPage : ContentPage
{
    private const int SectionCount = 5;

    private readonly IServiceProvider _services;
    private readonly View?[] _sections = new View?[SectionCount];
    private int _current = -1;

    public static MainPage? Current { get; private set; }

    public MainPage(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
        Bar.Selected = index => Show(index);
        Current = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
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
        if (index < 0 || index >= SectionCount) return;
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
