using Avalonia.Controls;
using Cartex.UI.Services;
using Cartex.UI.ViewModels;
using System.ComponentModel;

namespace Cartex.UI.Views;

public partial class SalesHistoryView : UserControl
{
    public SalesHistoryView()
    {
        InitializeComponent();
        UpdateHeaders();
        LocalizationManager.Instance.PropertyChanged += OnLanguageChanged;

        Loaded += async (_, _) =>
        {
            if (DataContext is SalesHistoryViewModel vm)
                await vm.LoadCommand.ExecuteAsync(null);
        };
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Item[]") UpdateHeaders();
    }

    private void UpdateHeaders()
    {
        var l = LocalizationManager.Instance;
        SalesGrid.Columns[1].Header = l["sale_date"];
        SalesGrid.Columns[2].Header = l["total"];
        SalesGrid.Columns[3].Header = l["cash"];
        SalesGrid.Columns[4].Header = l["card"];
        SalesGrid.Columns[5].Header = l["debt"];
        SalesGrid.Columns[6].Header = l["status"];
        SalesGrid.Columns[7].Header = l["customer"];
        SalesGrid.Columns[8].Header = l["sold_by"];
    }
}
