namespace Cartex.Shared.Models.Units;

public record CreateUnitRequest(
    string Name,
    string ShortName,
    string Dimension = "Count",
    decimal Factor = 1,
    decimal? DefaultQuantityStep = null,
    bool? DefaultAllowAmountEntry = null);
