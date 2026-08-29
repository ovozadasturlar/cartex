
namespace Cartex.UI.Services;

public static class AuditTranslator
{
    public static string TranslateAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action)) return string.Empty;
        var loc = LocalizationManager.Instance;
        return action.ToLowerInvariant() switch
        {
            "entitycreated" => loc["action_entitycreated"] ?? "Yaratildi",
            "entityupdated" => loc["action_entityupdated"] ?? "Tahrirlandi",
            "entitydeleted" => loc["action_entitydeleted"] ?? "O'chirildi",
            _ => action
        };
    }

    public static string TranslateTable(string? table)
    {
        if (string.IsNullOrWhiteSpace(table)) return string.Empty;
        var loc = LocalizationManager.Instance;
        var key = $"table_{table.ToLowerInvariant()}";
        var translated = loc[key];
        // If not translated, return original table name
        return translated != $"[{key}]" ? (translated ?? table) : table;
    }

    public static string TranslateField(string? field)
    {
        if (string.IsNullOrWhiteSpace(field)) return string.Empty;
        var loc = LocalizationManager.Instance;
        var key = $"field_{field.ToLowerInvariant()}";
        var translated = loc[key];
        return translated != $"[{key}]" ? (translated ?? field) : field;
    }
}
