using Cartex.Mobile.Store.ViewModels;

namespace Cartex.Mobile.Store.Views;

public partial class CaseSettlementPage : ContentPage
{
    private readonly CaseSettlementViewModel _viewModel;
    public CaseSettlementPage(CaseSettlementViewModel viewModel) { InitializeComponent(); BindingContext = _viewModel = viewModel; }
    protected override async void OnAppearing() { base.OnAppearing(); await _viewModel.AppearAsync(); }
    private void OnQuantityUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is Entry { BindingContext: CaseSettlementLine line }) _viewModel.CommitQuantityCommand.Execute(line);
    }
}
