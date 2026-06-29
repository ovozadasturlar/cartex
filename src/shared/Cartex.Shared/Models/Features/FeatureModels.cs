namespace Cartex.Shared.Models.Features;

public record FeatureDto(string Code, string Name, bool IsEnabled);

public record SetFeatureRequest(bool IsEnabled);
