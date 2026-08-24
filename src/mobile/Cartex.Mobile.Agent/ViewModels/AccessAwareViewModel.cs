using Cartex.Mobile.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cartex.Mobile.Agent.ViewModels;

public abstract class AccessAwareViewModel : ObservableObject
{
    private readonly AccessState _access;
    private readonly HashSet<string> _observedProperties = [];
    protected AccessState Access => _access;

    protected AccessAwareViewModel(AccessState access) => _access = access;

    protected void ObserveAccess(params string[] properties)
    {
        properties = properties.Where(_observedProperties.Add).ToArray();
        if (properties.Length == 0) return;
        var source = _access;
        var weak = new WeakReference<AccessAwareViewModel>(this);
        Action? handler = null;
        handler = () =>
        {
            if (!weak.TryGetTarget(out var target))
            {
                source.Changed -= handler;
                return;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                foreach (var property in properties)
                    target.OnPropertyChanged(property);
            });
        };
        source.Changed += handler;
    }
}
