using Cartex.Mobile.Store.ViewModels;

namespace Cartex.Mobile.Store.Views;

public partial class CaseReturnPage : ContentPage
{
    private readonly CaseReturnViewModel _viewModel;

    public CaseReturnPage(CaseReturnViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.AppearAsync();
    }

    private void OnQuantityUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is Entry { BindingContext: CaseReturnLine line })
            _viewModel.CommitQuantityCommand.Execute(line);
    }
}
