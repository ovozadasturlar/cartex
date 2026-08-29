using System.Buffers;
using System.Text;

namespace Cartex.Shared.Search;

public static class SearchFold
{
    private static readonly SearchValues<char> Apostrophes = SearchValues.Create("ʻʼ'‘’´");

    public static string Strict(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var result = new StringBuilder(value.Length);
        var separatorPending = false;

        foreach (var character in value.Normalize(NormalizationForm.FormC))
        {
            var mapped = Map(char.ToLowerInvariant(character));
            if (mapped is null)
            {
                separatorPending = result.Length > 0;
                continue;
            }

            if (mapped.Length == 0)
                continue;

            if (separatorPending)
            {
                result.Append(' ');
                separatorPending = false;
            }

            if (mapped == "'" && result.Length > 0 && result[^1] == '\'')
                continue;

            result.Append(mapped);
        }

        return result.ToString().TrimEnd();
    }

    public static string Fuzzy(string? value)
    {
        var strict = Strict(value);
        if (strict.Length == 0)
            return string.Empty;

        var result = new StringBuilder(strict.Length);
        foreach (var character in strict)
        {
            if (character == '\'')
                continue;

            result.Append(character switch
            {
                'q' => 'k',
                'x' => 'h',
                _ => character
            });
        }

        return result.ToString();
    }

    private static string? Map(char value)
    {
        if (Apostrophes.Contains(value))
            return "'";

        return value switch
        {
            'а' => "a",
            'б' => "b",
            'в' => "v",
            'г' => "g",
            'ғ' => "g'",
            'д' => "d",
            'е' => "e",
            'ё' => "yo",
            'ж' => "j",
            'з' => "z",
            'и' => "i",
            'й' => "y",
            'к' => "k",
            'қ' => "q",
            'л' => "l",
            'м' => "m",
            'н' => "n",
            'о' => "o",
            'п' => "p",
            'р' => "r",
            'с' => "s",
            'т' => "t",
            'у' => "u",
            'ў' => "o'",
            'ф' => "f",
            'х' => "x",
            'ҳ' => "h",
            'ц' => "ts",
            'ч' => "ch",
            'ш' => "sh",
            'щ' => "sch",
            'ъ' => "'",
            'ы' => "i",
            'ь' => string.Empty,
            'э' => "e",
            'ю' => "yu",
            'я' => "ya",
            _ when char.IsLetterOrDigit(value) => value.ToString(),
            _ => null
        };
    }
}
