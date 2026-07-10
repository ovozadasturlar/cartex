using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Cartex.UI.ViewModels;

namespace Cartex.UI;

public sealed class ViewLocator : IDataTemplate
{
    private static readonly Dictionary<Type, Control> _cache = new();

    public Control Build(object? param)
    {
        if (param is null) return new TextBlock { Text = "No view" };

        var vmType = param.GetType();
        if (_cache.TryGetValue(vmType, out var cached) && cached.Parent is null)
            return cached;

        var viewName = vmType.FullName!.Replace("ViewModel", "View").Replace(".ViewModels.", ".Views.");
        var viewType = vmType.Assembly.GetType(viewName);

        if (viewType is null)
            return new TextBlock { Text = $"View not found: {viewName}" };

        var view = (Control)Activator.CreateInstance(viewType)!;
        _cache[vmType] = view;
        return view;
    }

    public bool Match(object? data) => data is ViewModelBase;
}
