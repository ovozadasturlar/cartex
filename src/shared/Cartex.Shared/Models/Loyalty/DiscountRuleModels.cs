namespace Cartex.Shared.Models.Loyalty;

public record DiscountExceptionDto(string Scope, long TargetId, string TargetName);

public record DiscountRuleDto(long Id, string Name, bool IsEnabled, string Scope, long? TargetId, string? TargetName,
    long? CustomerId, string? CustomerName, decimal MinAmount, string Method, decimal Value, int Priority,
    DateOnly? StartsOn, DateOnly? EndsOn, List<DiscountExceptionDto> Exceptions);

public record DiscountExceptionInputDto(string Scope, long TargetId);

public record SaveDiscountRuleRequest(long Id, string Name, bool IsEnabled, string Scope, long? TargetId,
    long? CustomerId, decimal MinAmount, string Method, decimal Value, int Priority,
    DateOnly? StartsOn, DateOnly? EndsOn, List<DiscountExceptionInputDto>? Exceptions = null);

public record PreviewDiscountItemRequest(long VariantId, decimal Quantity, decimal UnitPrice);

public record PreviewDiscountRequest(long? CustomerId, List<PreviewDiscountItemRequest> Items);

public record DiscountApplicationDto(string Name, decimal Amount);

public record PreviewDiscountResultDto(decimal Total, List<DiscountApplicationDto> Applied);

public record LoyaltyStatsDto(int SalesCount, int DiscountedSales, decimal DiscountTotal, decimal GrossTotal, decimal BonusOutstanding);

public record ManufacturerDto(long Id, string Name);

public record SaveManufacturerRequest(string Name);
