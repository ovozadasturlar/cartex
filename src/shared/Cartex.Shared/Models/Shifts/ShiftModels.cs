using Cartex.Shared.Models.Common;

namespace Cartex.Shared.Models.Shifts;

public record ZReportCurrencyDto(string Currency, decimal OpeningFloat, decimal CashSales, decimal CashReturns, decimal DebtPayIn, decimal SupplyPayOut, decimal ExpectedCash, decimal CountedCash, decimal Difference);

public record CurrentShiftDto(long Id, DateTime OpenedAt, decimal OpeningFloat, decimal CashSales, decimal CashReturns, decimal PayIn, decimal PayOut, decimal DebtPayIn, decimal SupplyPayOut, decimal ExpectedCash)
{
    public decimal CardSales { get; init; }
    public decimal CardReturns { get; init; }
    public decimal BonusUsed { get; init; }
    public decimal NewDebtIssued { get; init; }
    public int SalesCount { get; init; }
    public List<ZReportCurrencyDto> Currencies { get; init; } = [];
}

public record ZReportDto(long ShiftId, decimal OpeningFloat, decimal CashSales, decimal CashReturns, decimal PayIn, decimal PayOut, decimal DebtPayIn, decimal SupplyPayOut, decimal ExpectedCash, decimal CountedCash, decimal Difference)
{
    public decimal CardSales { get; init; }
    public decimal CardReturns { get; init; }
    public decimal BonusUsed { get; init; }
    public decimal NewDebtIssued { get; init; }
    public int SalesCount { get; init; }
    public List<ZReportCurrencyDto> Currencies { get; init; } = [];
}

public record ShiftHistoryDto(long Id, long UserId, string UserName, DateTime OpenedAt, DateTime? ClosedAt, decimal OpeningFloat, decimal? CountedCash, string Status)
{
    public bool IsOpen => Status == "Open";
}

public record OpenShiftRequest(decimal OpeningFloat, List<CurrencyAmountDto>? Floats = null);

public record CloseShiftRequest(decimal CountedCash, List<CurrencyAmountDto>? Counted = null);

public record CashMovementRequest(decimal Amount, bool IsPayOut, string? Reason = null, long? ExpenseCategoryId = null);
