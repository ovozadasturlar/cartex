using Cartex.Shared.Models.Common;
using Cartex.Shared.Models.Stocks;

namespace Cartex.Shared.Models.Agents;

public record AgentCustomerDto(long Id, string FullName, string? Phone, string? Address, decimal DebtBalance, decimal CreditLimit, List<CurrencyAmountDto> DebtBalances);

public record AgentBootstrapDto(long? WarehouseId, string? WarehouseName, string BaseCurrency, DateTime ServerTime, List<AgentCustomerDto> Customers, List<StockOnHandDto> VanStock);
