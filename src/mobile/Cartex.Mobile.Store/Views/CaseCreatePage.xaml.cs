using Cartex.Mobile.Store.ViewModels;

namespace Cartex.Mobile.Store.Views;

public partial class CaseCreatePage : ContentPage
{
    private readonly CaseCreateViewModel _viewModel;
    public CaseCreatePage(CaseCreateViewModel viewModel) { InitializeComponent(); BindingContext = _viewModel = viewModel; }
    protected override async void OnAppearing() { base.OnAppearing(); await _viewModel.AppearAsync(); Title = _viewModel.PageTitle; }
    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.IsParticipantModalOpen)
        {
            _viewModel.CloseParticipantModalCommand.Execute(null);
            return true;
        }
        return base.OnBackButtonPressed();
    }
}
