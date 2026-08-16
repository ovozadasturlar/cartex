using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

public sealed class Party : SoftDeleteEntity
{
    public long BusinessId { get; set; }
    public Business Business { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? TaxId { get; set; }
    public string? Note { get; set; }
    public Customer? CustomerProfile { get; set; }
    public PartnerProfile? PartnerProfile { get; set; }
}

public sealed class PartnerProfile : AuditableEntity
{
    public long PartyId { get; set; }
    public Party Party { get; set; } = null!;
    public string PartnerCode { get; set; } = null!;
    public bool IsEnabled { get; set; } = true;
    public DateOnly JoinedAt { get; set; }
    public string? Note { get; set; }

    /// Consent is a fact about the person, kept apart from whether the shop is showing them right
    /// now: a shop may hide someone temporarily without revoking what they agreed to.
    public PublicConsentState PublicConsent { get; set; } = PublicConsentState.NotAsked;
    public DateTime? PublicConsentAt { get; set; }
    public long? PublicConsentByUserId { get; set; }
    public PublicConsentSource PublicConsentSource { get; set; } = PublicConsentSource.Staff;

    /// Agreeing to be listed is not the same as agreeing to publish a phone number, so the two
    /// are asked and stored separately.
    public bool PublicVisible { get; set; }
    public bool PublicPhoneVisible { get; set; }
    public string? PublicDisplayName { get; set; }
    public string? PublicAbout { get; set; }
}

public sealed class ParticipantRoleDefinition : AuditableEntity
{
    public long BusinessId { get; set; }
    public Business Business { get; set; } = null!;
    public string Key { get; set; } = null!;
    public string SingularLabel { get; set; } = null!;
    public string PluralLabel { get; set; } = null!;
    public bool IsEnabled { get; set; } = true;
    public bool IsRequired { get; set; }
    public bool CanEqualBuyer { get; set; } = true;
    public int MaxCount { get; set; } = 1;
    public bool AppliesToCart { get; set; } = true;
    public bool AppliesToSale { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class SaleParticipant : BaseEntity
{
    public long SaleId { get; set; }
    public Sale Sale { get; set; } = null!;
    public long RoleDefinitionId { get; set; }
    public ParticipantRoleDefinition RoleDefinition { get; set; } = null!;
    public long PartyId { get; set; }
    public Party Party { get; set; } = null!;
    public string PartyNameSnapshot { get; set; } = null!;
    public string? PartyPhoneSnapshot { get; set; }
    public string RoleLabelSnapshot { get; set; } = null!;
    public ParticipantAttributionSource Source { get; set; }
}

public sealed class CartParticipant : BaseEntity
{
    public long CartId { get; set; }
    public Cart Cart { get; set; } = null!;
    public long RoleDefinitionId { get; set; }
    public ParticipantRoleDefinition RoleDefinition { get; set; } = null!;
    public long PartyId { get; set; }
    public Party Party { get; set; } = null!;
    public string PartyNameSnapshot { get; set; } = null!;
    public string? PartyPhoneSnapshot { get; set; }
    public string RoleLabelSnapshot { get; set; } = null!;
}

public sealed class PartnerProgram : AuditableEntity
{
    public long BusinessId { get; set; }
    public Business Business { get; set; } = null!;
    public long? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public long RoleDefinitionId { get; set; }
    public ParticipantRoleDefinition RoleDefinition { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool IsEnabled { get; set; } = true;
    public PartnerRewardMode Mode { get; set; } = PartnerRewardMode.Points;
    public PartnerRewardBasis Basis { get; set; } = PartnerRewardBasis.NetRevenue;
    public PartnerRewardTrigger Trigger { get; set; } = PartnerRewardTrigger.Sale;
    public decimal Value { get; set; }
    public decimal? CapPerSale { get; set; }
    public int HoldDays { get; set; }
    public ICollection<PartnerRewardRule> Rules { get; set; } = [];
}

public sealed class PartnerRewardRule : BaseEntity
{
    public long PartnerProgramId { get; set; }
    public PartnerProgram Program { get; set; } = null!;
    public CashbackScope Scope { get; set; }
    public long TargetId { get; set; }
    public bool IsExcluded { get; set; }
    public decimal? ValueOverride { get; set; }
    public int Priority { get; set; }
}

public sealed class PartnerRewardEntry : AuditableEntity, IBranchScoped
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public long BranchId { get; set; }
    public long PartnerProfileId { get; set; }
    public PartnerProfile PartnerProfile { get; set; } = null!;
    public long PartnerProgramId { get; set; }
    public PartnerProgram PartnerProgram { get; set; } = null!;
    public long? SaleId { get; set; }
    public Sale? Sale { get; set; }
    public long? SaleItemId { get; set; }
    public SaleItem? SaleItem { get; set; }
    public long? CustomerReturnDocumentId { get; set; }
    public CustomerReturnDocument? CustomerReturnDocument { get; set; }
    public long? CustomerPaymentDocumentId { get; set; }
    public CustomerPaymentDocument? CustomerPaymentDocument { get; set; }
    public long? OriginalEntryId { get; set; }
    public PartnerRewardEntry? OriginalEntry { get; set; }
    public long? PartnerRedemptionDocumentId { get; set; }
    public PartnerRedemptionDocument? PartnerRedemptionDocument { get; set; }
    public PartnerRewardMode Mode { get; set; }
    public PartnerRewardState State { get; set; }
    public decimal Amount { get; set; }
    public decimal QuantityBasis { get; set; }
    public decimal FinancialBasis { get; set; }
    public DateTime AvailableAt { get; set; }
    public string? DetailsJson { get; set; }
}

public sealed class PartnerRedemptionDocument : AuditableEntity, IBranchScoped
{
    public long BranchId { get; set; }
    public long PartnerProfileId { get; set; }
    public PartnerProfile PartnerProfile { get; set; } = null!;
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public string DocumentNumber { get; set; } = null!;
    public DateOnly BusinessDate { get; set; }
    public BusinessDocumentStatus Status { get; set; } = BusinessDocumentStatus.Posted;
    public PartnerRewardMode Mode { get; set; }
    public decimal Amount { get; set; }
    public long? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }
    public decimal? ProductQuantity { get; set; }
    public string? Note { get; set; }
    public string? IdempotencyKey { get; set; }
    public ICollection<PartnerRewardEntry> Entries { get; set; } = [];
    public ICollection<Transaction> Transactions { get; set; } = [];
}
