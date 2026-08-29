using System.Collections.ObjectModel;
using System.Text.Json;
using Cartex.Shared.Models.AuditLogs;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;

namespace Cartex.UI.ViewModels;

public sealed record AuditChangeRow(string Field, string TranslatedField, string? OldValue, string? NewValue);

public partial class AuditDetailViewModel : ViewModelBase, IDialogContext
{
    public AuditLogDto Log { get; }
    public ObservableCollection<AuditChangeRow> Changes { get; } = [];

    public AuditDetailViewModel(AuditLogDto log)
    {
        Log = log;
        var oldValues = Parse(log.OldData);
        var newValues = Parse(log.NewData);
        foreach (var field in oldValues.Keys.Union(newValues.Keys).OrderBy(x => x))
            Changes.Add(new AuditChangeRow(
                field,
                Cartex.UI.Services.AuditTranslator.TranslateField(field),
                oldValues.GetValueOrDefault(field),
                newValues.GetValueOrDefault(field)));
    }

    public event EventHandler<object?>? RequestClose;

    [RelayCommand]
    private void CloseDialog() => RequestClose?.Invoke(this, null);

    public void Close() => RequestClose?.Invoke(this, null);

    private static Dictionary<string, string?> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new Dictionary<string, string?> { ["value"] = Display(document.RootElement) };
            return document.RootElement.EnumerateObject()
                .ToDictionary(x => x.Name, x => Display(x.Value));
        }
        catch
        {
            return new Dictionary<string, string?> { ["value"] = json };
        }
    }

    private static string? Display(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => value.GetRawText()
        };
}
