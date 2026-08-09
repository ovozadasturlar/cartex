using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.Partners;

namespace Cartex.Shared.Models.TradeCases;

public sealed record CreateTradeCaseRequest(
    long CustomerId,
    long WarehouseId,
    string Title,
    string? SiteAddress = null,
    string? Workflow = null,
    string? PricePolicy = null,
    string? Currency = null,
    DateOnly? BusinessDate = null,
    string? Note = null,
    string? IdempotencyKey = null,
    List<ParticipantSelectionRequest>? Participants = null);

public sealed record TradeCaseCreatedDto(long Id, string CaseNumber, int Version);

public sealed record UpdateTradeCaseRequest(
    string Title,
    string? SiteAddress = null,
    string? Note = null,
    List<ParticipantSelectionRequest>? Participants = null,
    int? ExpectedVersion = null);

public sealed record ChangeTradeCaseStatusRequest(string? Reason = null, int? ExpectedVersion = null);

public sealed record GoodsIssueLineRequest(long VariantId, decimal Quantity, decimal? UnitPrice = null);

public sealed record CreateGoodsIssueRequest(
    List<GoodsIssueLineRequest> Lines,
    DateOnly? BusinessDate = null,
    string? Note = null,
    string? IdempotencyKey = null,
    int? ExpectedCaseVersion = null);

public sealed record GoodsIssueCreatedDto(long Id, string DocumentNumber, decimal EstimatedAmount, int CaseVersion);

public sealed record GoodsReturnLineRequest(
    long GoodsIssueLineId,
    decimal Quantity,
    string? Reason,
    string Condition,
    string Disposition);

public sealed record CreateGoodsReturnRequest(
    List<GoodsReturnLineRequest> Lines,
    DateOnly? BusinessDate = null,
    string? Note = null,
    string? IdempotencyKey = null,
    int? ExpectedCaseVersion = null);

public sealed record GoodsReturnCreatedDto(long Id, string DocumentNumber, int CaseVersion);

public sealed record TradeCaseSettlementLineRequest(long GoodsIssueLineId, decimal Quantity);

public sealed record SettleTradeCaseRequest(
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidBonus,
    List<SalePaymentRequest>? Payments = null,
    List<TradeCaseSettlementLineRequest>? Lines = null,
    decimal DiscountAmount = 0,
    string? DebtCurrency = null,
    DateOnly? DebtDueDate = null,
    bool ApplyAutoDiscount = true,
    bool UseCustomerAdvance = true,
    bool CloseWhenEmpty = true,
    DateOnly? BusinessDate = null,
    string? Note = null,
    string? IdempotencyKey = null,
    int? ExpectedCaseVersion = null);

public sealed record TradeCaseSettlementCreatedDto(
    long Id,
    string DocumentNumber,
    long SaleId,
    string ReceiptToken,
    decimal Amount,
    bool CaseSettled,
    int CaseVersion);

public sealed record TradeCaseListDto(
    long Id,
    string CaseNumber,
    DateOnly BusinessDate,
    string Title,
    long CustomerId,
    string CustomerName,
    long WarehouseId,
    string WarehouseName,
    string Workflow,
    string PricePolicy,
    string Status,
    decimal IssuedQuantity,
    decimal ReturnedQuantity,
    decimal SettledQuantity,
    decimal CustodyQuantity,
    decimal EstimatedOutstandingAmount,
    string Currency,
    int Version,
    DateTime UpdatedAt);

public sealed record TradeCaseLineDto(
    long GoodsIssueLineId,
    long GoodsIssueDocumentId,
    string IssueDocumentNumber,
    DateOnly IssueDate,
    long VariantId,
    string ProductName,
    string UnitName,
    decimal IssuedQuantity,
    decimal ReturnedQuantity,
    decimal SettledQuantity,
    decimal CustodyQuantity,
    decimal UnitPrice,
    string PriceCurrency,
    decimal PriceRate,
    string? Barcode,
    decimal QuantityStep = 1,
    bool AllowsFractional = false);

public sealed record TradeCaseDocumentDto(
    string Type,
    long Id,
    string DocumentNumber,
    DateOnly BusinessDate,
    DateTime CreatedAt,
    string Status,
    decimal Quantity,
    decimal Amount,
    string? Note,
    long? SaleId = null,
    string? ReceiptToken = null);

public sealed record TradeCaseAllowedActions(
    bool CanEdit,
    bool CanIssue,
    bool CanReturn,
    bool CanSettle,
    bool CanCancel,
    bool CanReceivePayment,
    bool CanExportStatement,
    bool CanClose = false);

public sealed record TradeCaseParticipantDto(
    long RoleDefinitionId,
    long PartyId,
    string RoleLabel,
    string PartyName,
    string? PartyPhone);

public sealed record TradeCaseDetailDto(
    long Id,
    string CaseNumber,
    DateOnly BusinessDate,
    string Title,
    string? SiteAddress,
    long CustomerId,
    string CustomerName,
    string? CustomerPhone,
    long BranchId,
    string BranchName,
    long WarehouseId,
    string WarehouseName,
    string Workflow,
    string PricePolicy,
    string Status,
    string Currency,
    string? Note,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<TradeCaseLineDto> Lines,
    IReadOnlyList<TradeCaseDocumentDto> Documents,
    TradeCaseAllowedActions AllowedActions,
    IReadOnlyList<TradeCaseParticipantDto>? Participants = null);

public sealed record TradeCaseStatementEntryDto(
    DateTime OccurredAt,
    string Type,
    long DocumentId,
    string DocumentNumber,
    string Summary,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance,
    string Currency,
    long? SaleId = null,
    string? ReceiptToken = null);

public sealed record TradeCaseStatementProductDto(
    long VariantId,
    string ProductName,
    string UnitName,
    decimal Issued,
    decimal ReturnedSellable,
    decimal ReturnedNonSellable,
    decimal Settled,
    decimal OutstandingCustody,
    decimal AverageUnitPrice,
    decimal ChargedAmount);

public sealed record TradeCaseStatementDto(
    long TradeCaseId,
    string CaseNumber,
    string CaseTitle,
    long CustomerId,
    string CustomerName,
    DateTime? From,
    DateTime? To,
    string Currency,
    decimal OpeningBalance,
    decimal ClosingBalance,
    IReadOnlyList<TradeCaseStatementEntryDto> Timeline,
    IReadOnlyList<TradeCaseStatementProductDto> Products,
    DateTime GeneratedAt);
