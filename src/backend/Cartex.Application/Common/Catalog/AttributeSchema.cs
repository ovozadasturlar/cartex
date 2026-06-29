using System.Text.Json;
using Cartex.Domain.Common.Exceptions;

namespace Cartex.Application.Common.Catalog;

public record AttributeFieldDef(
    string Key,
    string? Label,
    string Type,
    bool Required,
    List<string>? Options,
    string? Suffix,
    bool VariantDefining);

public static class AttributeSchema
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<AttributeFieldDef> Parse(string? schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
            return [];
        return JsonSerializer.Deserialize<List<AttributeFieldDef>>(schemaJson, Json) ?? [];
    }

    public static void Validate(string? schemaJson, string? valuesJson)
    {
        var fields = Parse(schemaJson);
        if (fields.Count == 0)
            return;

        Dictionary<string, JsonElement> values = string.IsNullOrWhiteSpace(valuesJson)
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(valuesJson, Json) ?? [];

        foreach (var field in fields)
        {
            var has = values.TryGetValue(field.Key, out var value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);

            if (!has)
            {
                if (field.Required)
                    throw new BusinessRuleException($"Atribut '{field.Label ?? field.Key}' to'ldirilishi shart.");
                continue;
            }

            switch (field.Type)
            {
                case "number" when value.ValueKind != JsonValueKind.Number:
                    throw new BusinessRuleException($"Atribut '{field.Label ?? field.Key}' raqam bo'lishi kerak.");
                case "bool" when value.ValueKind is not (JsonValueKind.True or JsonValueKind.False):
                    throw new BusinessRuleException($"Atribut '{field.Label ?? field.Key}' true/false bo'lishi kerak.");
                case "select" when field.Options is { Count: > 0 } && !field.Options.Contains(value.ToString()):
                    throw new BusinessRuleException($"Atribut '{field.Label ?? field.Key}' uchun noto'g'ri qiymat.");
            }
        }
    }
}
