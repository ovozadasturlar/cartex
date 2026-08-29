using Cartex.Mobile.Store.ViewModels;

namespace Cartex.Mobile.Store.Views;

public partial class TradeView : ContentView, ISectionView, IArgumentAware
{
    private readonly TradeViewModel _vm;

    public TradeView(TradeViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    public void Appear() => _ = _vm.AppearAsync();

    public void Disappear() => _vm.Disappear();

    public void Apply(string argument) =>
        _vm.ApplyQueryAttributes(new Dictionary<string, object> { ["section"] = argument });
}
