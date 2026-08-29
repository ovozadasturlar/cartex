using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.UI.Services;
using Irihi.Avalonia.Shared.Contracts;

namespace Cartex.UI.ViewModels;

public sealed partial class OfflineImportRow(OfflineExportEvent source, string kindText, string? details) : ObservableObject
{
    public OfflineExportEvent Source { get; } = source;
    public string KindText { get; } = kindText;
    public string When { get; } = source.OccurredAt.ToLocalTime().ToString("dd.MM HH:mm");
    public string? Details { get; } = details;
    [ObservableProperty] private bool _isSelected = true;
}

public sealed record OfflineImportChoice(List<Guid> SkipEventIds, bool SkipRejected);

public partial class OfflineImportDialogViewModel(List<OfflineImportRow> rows) : ViewModelBase, IDialogContext
{
    public List<OfflineImportRow> Rows { get; } = rows;

    [ObservableProperty] private bool _skipRejected;

    public event EventHandler<object?>? RequestClose;

    [RelayCommand]
    private void Accept() => RequestClose?.Invoke(this, new OfflineImportChoice(
        Rows.Where(x => !x.IsSelected).Select(x => Guid.Parse(x.Source.EventId)).ToList(),
        SkipRejected));

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, null);

    public void Close() => RequestClose?.Invoke(this, null);
}
