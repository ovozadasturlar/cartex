namespace Cartex.Shared.Models.Features;

public record FeatureDto(string Code, string Name, bool IsEnabled);

public record SetFeatureRequest(bool IsEnabled);

public sealed record OwnerModuleDto(string Code, string Name, bool Available, bool IsEnabled)
{
    public bool IsActive => Available && IsEnabled;
}
