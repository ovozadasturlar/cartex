namespace Cartex.Shared.Models.Partners;

public sealed record ParticipantSelectionRequest(long RoleDefinitionId, long PartyId);

public sealed record CreatePartnerRequest(
    string FullName,
    string? Phone = null,
    string? Email = null,
    string? Address = null,
    long? CustomerId = null,
    string? Note = null);

public sealed record UpdatePartnerRequest(
    string FullName,
    string? Phone = null,
    string? Email = null,
    string? Address = null,
    bool IsEnabled = true,
    string? Note = null);

/// Consent state as recorded, not as displayed: "NotAsked", "Granted", "Declined", "Withdrawn".
public sealed record SetPartnerPublicityRequest(
    string Consent,
    bool PublicVisible = false,
    bool PublicPhoneVisible = false,
    string? PublicDisplayName = null,
    string? PublicAbout = null);

public sealed record SetCustomerPartnershipRequest(bool IsPartner);

/// Partnership as the customer profile sees it: a flag on the person plus what they agreed to have
/// published. The reward figures are deliberately absent — the score is an internal tool (HAMKOR-10).
public sealed record CustomerPartnerDto(
    long PartnerId,
    bool IsEnabled,
    string PublicConsent,
    bool PublicVisible,
    bool PublicPhoneVisible,
    string? PublicDisplayName,
    string? PublicAbout);

public sealed record PartnerDto(
    long Id,
    long PartyId,
    string PartnerCode,
    string FullName,
    string? Phone,
    string? Email,
    string? Address,
    long? CustomerId,
    bool IsEnabled,
    DateOnly JoinedAt,
    decimal Earned,
    decimal Pending,
    decimal Redeemed,
    decimal Score,
    string? Note,
    string PublicConsent = "NotAsked",
    bool PublicVisible = false,
    bool PublicPhoneVisible = false,
    string? PublicDisplayName = null,
    string? PublicAbout = null);

public sealed record SaveParticipantRoleRequest(
    long? Id,
    string Key,
    string SingularLabel,
    string PluralLabel,
    bool IsEnabled = true,
    bool IsRequired = false,
    bool CanEqualBuyer = true,
    int MaxCount = 1,
    bool AppliesToCart = true,
    bool AppliesToSale = true,
    int SortOrder = 0);

public sealed record ParticipantRoleDto(
    long Id,
    string Key,
    string SingularLabel,
    string PluralLabel,
    bool IsEnabled,
    bool IsRequired,
    bool CanEqualBuyer,
    int MaxCount,
    bool AppliesToCart,
    bool AppliesToSale,
    int SortOrder);

public sealed record PartnerRewardRuleRequest(
    string Scope,
    long TargetId,
    bool IsExcluded = false,
    decimal? ValueOverride = null,
    int Priority = 0);

public sealed record SavePartnerProgramRequest(
    long? Id,
    long RoleDefinitionId,
    string Name,
    bool IsEnabled,
    string Mode,
    string Basis,
    string Trigger,
    decimal Value,
    long? BranchId = null,
    decimal? CapPerSale = null,
    int HoldDays = 0,
    List<PartnerRewardRuleRequest>? Rules = null);

public sealed record PartnerProgramDto(
    long Id,
    long RoleDefinitionId,
    string RoleLabel,
    string Name,
    bool IsEnabled,
    string Mode,
    string Basis,
    string Trigger,
    decimal Value,
    long? BranchId,
    string? BranchName,
    decimal? CapPerSale,
    int HoldDays,
    IReadOnlyList<PartnerRewardRuleRequest> Rules);

public sealed record PartnerRankingDto(
    int Rank,
    long PartnerId,
    long PartyId,
    string PartnerCode,
    string FullName,
    decimal Earned,
    decimal Pending,
    decimal Redeemed,
    decimal Score,
    int SalesCount);

public sealed record PartnerRewardEntryDto(
    long Id,
    DateTime OccurredAt,
    string Mode,
    string State,
    decimal Amount,
    DateTime AvailableAt,
    long? SaleId,
    string? ProductName,
    long? CustomerPaymentDocumentId,
    long? CustomerReturnDocumentId,
    long? RedemptionDocumentId,
    string ProgramName,
    string? DetailsJson);

public sealed record PartnerRedemptionRequest(
    string Mode,
    decimal Amount,
    long? BranchId = null,
    long? WarehouseId = null,
    long? ProductVariantId = null,
    decimal? ProductQuantity = null,
    DateOnly? BusinessDate = null,
    string? Note = null,
    string? IdempotencyKey = null);

public sealed record PartnerRedemptionCreatedDto(long Id, string DocumentNumber, decimal Amount);

public sealed record PartnerRewardAdjustmentRequest(
    long ProgramId,
    decimal Amount,
    long? BranchId = null,
    string? Note = null,
    Guid? EventId = null);

public sealed record PartnerRewardAdjustmentResult(long Id, Guid EventId, decimal Amount);
