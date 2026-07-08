using System.Text.Json;
using System.Text.Json.Nodes;
using Cartex.UI.Models;

namespace Cartex.UI.Services;

public static class AttributeSchemaCodec
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record FieldDef(string Key, string? Label, string Type, bool Required, List<string>? Options, string? Suffix, bool VariantDefining);

    public static List<AttributeFieldRow> ParseSchema(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        var defs = JsonSerializer.Deserialize<List<FieldDef>>(json, Json) ?? [];
        return defs.Select(d => new AttributeFieldRow
        {
            Key = d.Key,
            Label = d.Label ?? "",
            Type = d.Type,
            Required = d.Required,
            Options = d.Options is null ? "" : string.Join(", ", d.Options),
            Suffix = d.Suffix ?? "",
            VariantDefining = d.VariantDefining
        }).ToList();
    }

    public static string? SerializeSchema(IEnumerable<AttributeFieldRow> rows)
    {
        var defs = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Key))
            .Select(r => new FieldDef(
                r.Key.Trim(),
                string.IsNullOrWhiteSpace(r.Label) ? null : r.Label.Trim(),
                r.Type,
                r.Required,
                r.Type == "select"
                    ? r.Options.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                    : null,
                string.IsNullOrWhiteSpace(r.Suffix) ? null : r.Suffix.Trim(),
                r.VariantDefining))
            .ToList();
        return defs.Count == 0 ? null : JsonSerializer.Serialize(defs, Json);
    }

    public static List<ProductAttributeVM> BuildValueFields(string? schemaJson, string? valuesJson)
    {
        var defs = string.IsNullOrWhiteSpace(schemaJson) ? [] : JsonSerializer.Deserialize<List<FieldDef>>(schemaJson, Json) ?? [];
        var values = string.IsNullOrWhiteSpace(valuesJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(valuesJson, Json);

        var result = new List<ProductAttributeVM>();
        foreach (var d in defs)
        {
            var vm = new ProductAttributeVM
            {
                Key = d.Key,
                Display = (d.Label ?? d.Key) + (d.Required ? " *" : ""),
                Type = d.Type,
                Required = d.Required,
                Suffix = d.Suffix
            };
            if (d.Options is not null)
                foreach (var o in d.Options) vm.Options.Add(o);

            if (values is not null && values.TryGetValue(d.Key, out var v) && v.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
            {
                switch (d.Type)
                {
                    case "number" when v.ValueKind == JsonValueKind.Number: vm.NumberValue = v.GetDecimal(); break;
                    case "bool" when v.ValueKind is JsonValueKind.True or JsonValueKind.False: vm.BoolValue = v.GetBoolean(); break;
                    case "select": vm.SelectValue = v.ToString(); break;
                    default: vm.TextValue = v.ToString(); break;
                }
            }
            result.Add(vm);
        }
        return result;
    }

    public static string? SerializeValues(IEnumerable<ProductAttributeVM> fields)
    {
        var obj = new JsonObject();
        foreach (var f in fields)
        {
            switch (f.Type)
            {
                case "number" when f.NumberValue.HasValue: obj[f.Key] = f.NumberValue.Value; break;
                case "bool": obj[f.Key] = f.BoolValue; break;
                case "select" when !string.IsNullOrWhiteSpace(f.SelectValue): obj[f.Key] = f.SelectValue; break;
                case "text" when !string.IsNullOrWhiteSpace(f.TextValue): obj[f.Key] = f.TextValue.Trim(); break;
            }
        }
        return obj.Count == 0 ? null : obj.ToJsonString();
    }
}
