using System.Collections.ObjectModel;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Store.ViewModels;

public sealed partial class OfflineImportRow(MobileOfflineExportEvent item, string title, string? summary) : ObservableObject
{
    [ObservableProperty] private bool _selected = true;
    public MobileOfflineExportEvent Item { get; } = item;
    public string Title { get; } = title;
    public string? Summary { get; } = summary;
}

public partial class OfflineImportViewModel(MobileOfflineService offline) : ObservableObject, IQueryAttributable
{
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _skipRejected;
    public ObservableCollection<OfflineImportRow> Rows { get; } = [];
    private MobileOfflineExportFile? _file;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("file", out var value) || value is not MobileOfflineExportFile file) return;
        _file = file;
        Rows.Clear();
        foreach (var item in file.Events.OrderBy(x => x.Sequence))
            Rows.Add(new OfflineImportRow(item,
                $"{OfflineSettingsViewModel.KindLabel(item.Kind)} · {item.OccurredAt.ToLocalTime():dd.MM HH:mm}",
                PayloadSummary(item)));
    }

    private static string? PayloadSummary(MobileOfflineExportEvent item)
    {
        try
        {
            var root = item.Payload;
            switch (item.Kind)
            {
                case "customer.payment.create":
                    return root.GetProperty("tenders").EnumerateArray()
                        .Sum(x => x.GetProperty("amount").GetDecimal()).ToString("N0");
                case "supply.create":
                {
                    var items = root.GetProperty("items");
                    var total = items.EnumerateArray()
                        .Sum(x => x.GetProperty("quantity").GetDecimal() * x.GetProperty("purchasePrice").GetDecimal());
                    return $"{items.GetArrayLength()} × • {total:N0}";
                }
                default:
                {
                    var total = root.GetProperty("paidCash").GetDecimal() + root.GetProperty("paidCard").GetDecimal();
                    return $"{root.GetProperty("items").GetArrayLength()} × • {total:N0}";
                }
            }
        }
        catch
        {
            return null;
        }
    }

    [RelayCommand]
    private async Task UploadAsync()
    {
        if (_file is null || IsBusy) return;
        IsBusy = true;
        try
        {
            var skipIds = Rows.Where(x => !x.Selected)
                .Select(x => Guid.Parse(x.Item.EventId)).ToList();
            var results = await offline.ImportFileAsync(
                _file, SkipRejected, skipIds.Count == 0 ? null : skipIds);
            var rejected = results.Where(x => x.Status == "Rejected").ToList();
            var deferred = results.Count(x => x.Status == "Deferred");
            var skipped = results.Count(x => x.Status == "Skipped");
            var text = $"{Loc.Instance["offline_import_applied"]}: {results.Count(x => x.Status == "Applied")}\n" +
                       $"{Loc.Instance["offline_import_already"]}: {results.Count(x => x.Status == "AlreadyApplied")}" +
                       (skipped > 0 ? $"\n{Loc.Instance["offline_discard"]}: {skipped}" : "") +
                       (deferred > 0 ? $"\n{Loc.Instance["offline_import_deferred"]}: {deferred}" : "");
            if (rejected.Count > 0)
                text += $"\n{Loc.Instance["error"]}: {rejected.Count}\n" +
                        string.Join("\n", rejected.Take(5).Select(x => $"• №{x.Sequence} — {x.Error}"));
            await Shell.Current.CurrentPage.DisplayAlertAsync(
                Loc.Instance["offline_import"], text, Loc.Instance["ok"]);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            Ui.Toast(ex is Refit.ApiException api ? ApiErrors.Describe(api) : ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
