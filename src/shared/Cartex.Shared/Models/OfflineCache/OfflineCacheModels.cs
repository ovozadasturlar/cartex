using Cartex.Shared.Models.Stocks;

namespace Cartex.Shared.Models.OfflineCache;

public record OfflineCacheStateDto(string? DeviceId, string? DeviceName, DateTime? ClaimedAt);

public record ClaimOfflineCacheRequest(string DeviceId, string DeviceName);

public record OfflineBarcodeDto(long VariantId, string Code, decimal PackQty);

public record OfflineCustomerDto(long Id, string FullName, string? Phone, string? CardBarcode, decimal DiscountPct, decimal DebtBalance, decimal CreditLimit);

public record OfflineSnapshotDto(string BaseCurrency, DateTime ServerTime, List<StockOnHandDto> Products, List<OfflineBarcodeDto> Barcodes, List<OfflineCustomerDto> Customers);
