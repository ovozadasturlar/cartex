namespace Cartex.Shared.Models.Units;

public record UnitDto(long Id, string Name, string ShortName, string Dimension, decimal Factor, bool IsSystem, bool IsEnabled = true, bool IsDefault = false);
