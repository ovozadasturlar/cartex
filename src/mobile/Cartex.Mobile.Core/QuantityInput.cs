using System.Globalization;

namespace Cartex.Mobile.Core;

public enum QuantityInputError
{
    None,
    Invalid,
    MustBePositive,
    FractionNotAllowed
}

public static class QuantityInput
{
    private const int MaxDecimalDigits = 3;

    public static bool TryParse(
        string? input,
        bool allowsFractional,
        out decimal quantity,
        out QuantityInputError error)
    {
        quantity = 0;
        error = QuantityInputError.Invalid;

        var normalized = input?.Trim().Replace(" ", string.Empty);
        if (string.IsNullOrEmpty(normalized) ||
            (normalized.Contains('.') && normalized.Contains(',')))
            return false;

        normalized = normalized.Replace(',', '.');
        var separator = normalized.IndexOf('.');
        if (separator >= 0 && normalized.Length - separator - 1 > MaxDecimalDigits)
            return false;

        if (!decimal.TryParse(
                normalized,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out quantity))
            return false;

        quantity = decimal.Round(quantity, MaxDecimalDigits, MidpointRounding.AwayFromZero);
        if (quantity <= 0)
        {
            error = QuantityInputError.MustBePositive;
            return false;
        }

        if (!allowsFractional && quantity != decimal.Truncate(quantity))
        {
            error = QuantityInputError.FractionNotAllowed;
            return false;
        }

        error = QuantityInputError.None;
        return true;
    }

    public static bool IsValid(decimal quantity, bool allowsFractional) =>
        quantity > 0 && quantity == decimal.Round(quantity, MaxDecimalDigits) &&
        (allowsFractional || quantity == decimal.Truncate(quantity));

    public static decimal NormalizeStep(bool allowsFractional) => allowsFractional ? 0.001m : 1m;

    public static string Format(decimal quantity) => quantity.ToString("0.###", CultureInfo.CurrentCulture);
}
