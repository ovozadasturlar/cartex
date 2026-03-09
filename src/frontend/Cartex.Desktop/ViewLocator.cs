using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Cartex.Desktop.ViewModels;

namespace Cartex.Desktop;

public sealed class ViewLocator : IDataTemplate
{
    public Control Build(object? param)
    {
        if (param is null) return new TextBlock { Text = "No view" };

        var vmType = param.GetType();
        var viewName = vmType.FullName!.Replace("ViewModel", "View").Replace(".ViewModels.", ".Views.");
        var viewType = vmType.Assembly.GetType(viewName);

        if (viewType is not null)
            return (Control)Activator.CreateInstance(viewType)!;

        return new TextBlock { Text = $"View not found: {viewName}" };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
