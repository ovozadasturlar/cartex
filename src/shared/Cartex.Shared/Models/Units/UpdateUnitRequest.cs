namespace Cartex.Shared.Models.Units;

public record UpdateUnitRequest(
    string Name,
    string ShortName,
    string Dimension,
    decimal Factor,
    bool? AllowFractional = null,
    bool? DefaultAllowAmountEntry = null);
