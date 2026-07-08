using Avalonia.Input;
using Cartex.UI.Models;

namespace Cartex.UI.Services;

public sealed class ShortcutService
{
    private object? _owner;

    public IReadOnlyList<PageShortcut> Current { get; private set; } = [];

    public event Action? Changed;

    public void SetScope(object owner, IReadOnlyList<PageShortcut> shortcuts)
    {
        _owner = owner;
        Current = shortcuts;
        Changed?.Invoke();
    }

    public void ClearScope(object owner)
    {
        if (!ReferenceEquals(_owner, owner)) return;
        _owner = null;
        Current = [];
        Changed?.Invoke();
    }

    public bool TryHandle(KeyEventArgs e, bool inTextInput)
    {
        var mods = e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt | KeyModifiers.Meta);
        foreach (var shortcut in Current)
        {
            if (shortcut.Key != e.Key || shortcut.Modifiers != mods) continue;
            if (inTextInput && !shortcut.WorksInText) continue;
            if (shortcut.IsEnabled is { } enabled && !enabled()) continue;
            shortcut.Execute();
            return true;
        }
        return false;
    }
}
