using System.Text;

namespace Cartex.Mobile.Core;

public static class UzText
{
    private static readonly Dictionary<char, string> Map = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "yo",
        ['ж'] = "j", ['з'] = "z", ['и'] = "i", ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m",
        ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u",
        ['ф'] = "f", ['х'] = "x", ['ц'] = "ts", ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "sh",
        ['ъ'] = "'", ['ы'] = "i", ['ь'] = "", ['э'] = "e", ['ю'] = "yu", ['я'] = "ya",
        ['қ'] = "q", ['ғ'] = "g'", ['ў'] = "o'", ['ҳ'] = "h",
        ['А'] = "A", ['Б'] = "B", ['В'] = "V", ['Г'] = "G", ['Д'] = "D", ['Е'] = "E", ['Ё'] = "Yo",
        ['Ж'] = "J", ['З'] = "Z", ['И'] = "I", ['Й'] = "Y", ['К'] = "K", ['Л'] = "L", ['М'] = "M",
        ['Н'] = "N", ['О'] = "O", ['П'] = "P", ['Р'] = "R", ['С'] = "S", ['Т'] = "T", ['У'] = "U",
        ['Ф'] = "F", ['Х'] = "X", ['Ц'] = "Ts", ['Ч'] = "Ch", ['Ш'] = "Sh", ['Щ'] = "Sh",
        ['Ъ'] = "'", ['Ы'] = "I", ['Ь'] = "", ['Э'] = "E", ['Ю'] = "Yu", ['Я'] = "Ya",
        ['Қ'] = "Q", ['Ғ'] = "G'", ['Ў'] = "O'", ['Ҳ'] = "H"
    };

    public static string ToLatin(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (Map.TryGetValue(c, out var m)) sb.Append(m);
            else sb.Append(c);
        }
        return sb.ToString();
    }

    public static string Fold(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var c in ToLatin(s).ToLowerInvariant())
        {
            if (c is '\'' or 'ʻ' or 'ʼ' or '’' or '`') continue;
            if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)) sb.Append(c);
        }
        return sb.ToString();
    }
}
