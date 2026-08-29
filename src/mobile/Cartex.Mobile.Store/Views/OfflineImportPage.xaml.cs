using Cartex.Mobile.Store.ViewModels;

namespace Cartex.Mobile.Store.Views;

public partial class OfflineImportPage : ContentPage
{
    public OfflineImportPage(OfflineImportViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
