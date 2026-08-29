using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cartex.Persistence.Configurations;

public sealed class PartyConfiguration : IEntityTypeConfiguration<Party>
{
    public void Configure(EntityTypeBuilder<Party> builder)
    {
        builder.ToTable("parties");
        builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.SearchFold, "ix_parties_search_fold_trgm").HasDatabaseName("ix_parties_search_fold_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
        builder.Property(x => x.Phone).HasMaxLength(20);
        builder.Property(x => x.Email).HasMaxLength(120);
        builder.Property(x => x.Address).HasMaxLength(300);
        builder.Property(x => x.TaxId).HasMaxLength(30);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.HasOne(x => x.Business).WithMany().HasForeignKey(x => x.BusinessId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.BusinessId, x.Phone }).IsUnique()
            .HasFilter("\"phone\" IS NOT NULL AND NOT \"is_deleted\"");
        builder.HasIndex(x => new { x.BusinessId, x.FullName });
    }
}

public sealed class PartnerProfileConfiguration : IEntityTypeConfiguration<PartnerProfile>
{
    public void Configure(EntityTypeBuilder<PartnerProfile> builder)
    {
        builder.ToTable("partner_profiles");
        builder.Property(x => x.PartnerCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.PublicConsent).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.PublicConsentSource).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.PublicDisplayName).HasMaxLength(120);
        builder.Property(x => x.PublicAbout).HasMaxLength(600);
        builder.HasIndex(x => x.PublicVisible);
        builder.HasIndex(x => x.PartyId).IsUnique();
        builder.HasIndex(x => x.PartnerCode).IsUnique();
        builder.HasOne(x => x.Party).WithOne(x => x.PartnerProfile)
            .HasForeignKey<PartnerProfile>(x => x.PartyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ParticipantRoleDefinitionConfiguration : IEntityTypeConfiguration<ParticipantRoleDefinition>
{
    public void Configure(EntityTypeBuilder<ParticipantRoleDefinition> builder)
    {
        builder.ToTable("participant_role_definitions", t =>
            t.HasCheckConstraint("ck_participant_roles_max_count", "\"max_count\" BETWEEN 1 AND 10"));
        builder.Property(x => x.Key).HasMaxLength(40).IsRequired();
        builder.Property(x => x.SingularLabel).HasMaxLength(40).IsRequired();
        builder.Property(x => x.PluralLabel).HasMaxLength(60).IsRequired();
        builder.HasIndex(x => new { x.BusinessId, x.Key }).IsUnique();
        builder.HasIndex(x => new { x.BusinessId, x.IsEnabled, x.SortOrder });
        builder.HasOne(x => x.Business).WithMany().HasForeignKey(x => x.BusinessId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SaleParticipantConfiguration : IEntityTypeConfiguration<SaleParticipant>
{
    public void Configure(EntityTypeBuilder<SaleParticipant> builder)
    {
        builder.ToTable("sale_participants");
        ConfigureSnapshot(builder);
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => new { x.SaleId, x.RoleDefinitionId, x.PartyId }).IsUnique();
        builder.HasOne(x => x.Sale).WithMany(x => x.Participants).HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.RoleDefinition).WithMany().HasForeignKey(x => x.RoleDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Party).WithMany().HasForeignKey(x => x.PartyId).OnDelete(DeleteBehavior.Restrict);
    }

    internal static void ConfigureSnapshot<T>(EntityTypeBuilder<T> builder) where T : class
    {
        builder.Property("PartyNameSnapshot").HasMaxLength(200);
        builder.Property("PartyPhoneSnapshot").HasMaxLength(20);
        builder.Property("RoleLabelSnapshot").HasMaxLength(40);
    }
}

public sealed class CartParticipantConfiguration : IEntityTypeConfiguration<CartParticipant>
{
    public void Configure(EntityTypeBuilder<CartParticipant> builder)
    {
        builder.ToTable("cart_participants");
        SaleParticipantConfiguration.ConfigureSnapshot(builder);
        builder.HasIndex(x => new { x.CartId, x.RoleDefinitionId, x.PartyId }).IsUnique();
        builder.HasOne(x => x.Cart).WithMany(x => x.Participants).HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.RoleDefinition).WithMany().HasForeignKey(x => x.RoleDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Party).WithMany().HasForeignKey(x => x.PartyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PartnerProgramConfiguration : IEntityTypeConfiguration<PartnerProgram>
{
    public void Configure(EntityTypeBuilder<PartnerProgram> builder)
    {
        builder.ToTable("partner_programs", t =>
        {
            t.HasCheckConstraint("ck_partner_program_value", "\"value\" >= 0");
            t.HasCheckConstraint("ck_partner_program_hold", "\"hold_days\" BETWEEN 0 AND 3650");
        });
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Mode).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.Basis).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Trigger).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.Value).HasPrecision(18, 4);
        builder.Property(x => x.CapPerSale).HasPrecision(18, 4);
        builder.HasIndex(x => new { x.BusinessId, x.RoleDefinitionId, x.BranchId, x.IsEnabled });
        builder.HasOne(x => x.Business).WithMany().HasForeignKey(x => x.BusinessId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RoleDefinition).WithMany().HasForeignKey(x => x.RoleDefinitionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PartnerRewardRuleConfiguration : IEntityTypeConfiguration<PartnerRewardRule>
{
    public void Configure(EntityTypeBuilder<PartnerRewardRule> builder)
    {
        builder.ToTable("partner_reward_rules");
        builder.Property(x => x.Scope).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.ValueOverride).HasPrecision(18, 4);
        builder.HasIndex(x => new { x.PartnerProgramId, x.Scope, x.TargetId }).IsUnique();
        builder.HasOne(x => x.Program).WithMany(x => x.Rules).HasForeignKey(x => x.PartnerProgramId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PartnerRewardEntryConfiguration : IEntityTypeConfiguration<PartnerRewardEntry>
{
    public void Configure(EntityTypeBuilder<PartnerRewardEntry> builder)
    {
        builder.ToTable("partner_reward_entries");
        builder.Property(x => x.Mode).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.State).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.Amount).HasPrecision(18, 4);
        builder.Property(x => x.QuantityBasis).HasPrecision(12, 3);
        builder.Property(x => x.FinancialBasis).HasPrecision(18, 2);
        builder.Property(x => x.DetailsJson).HasColumnType("jsonb");
        builder.HasIndex(x => x.EventId).IsUnique();
        builder.HasIndex(x => new { x.PartnerProfileId, x.Mode, x.State, x.AvailableAt });
        builder.HasIndex(x => new { x.SaleItemId, x.PartnerProgramId });
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PartnerProfile).WithMany().HasForeignKey(x => x.PartnerProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PartnerProgram).WithMany().HasForeignKey(x => x.PartnerProgramId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Sale).WithMany().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SaleItem).WithMany().HasForeignKey(x => x.SaleItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CustomerReturnDocument).WithMany().HasForeignKey(x => x.CustomerReturnDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CustomerPaymentDocument).WithMany().HasForeignKey(x => x.CustomerPaymentDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OriginalEntry).WithMany().HasForeignKey(x => x.OriginalEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PartnerRedemptionDocument).WithMany(x => x.Entries)
            .HasForeignKey(x => x.PartnerRedemptionDocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PartnerRedemptionDocumentConfiguration : IEntityTypeConfiguration<PartnerRedemptionDocument>
{
    public void Configure(EntityTypeBuilder<PartnerRedemptionDocument> builder)
    {
        builder.ToTable("partner_redemption_documents", t =>
            t.HasCheckConstraint("ck_partner_redemptions_amount", "\"amount\" > 0"));
        builder.Property(x => x.DocumentNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.Mode).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.Amount).HasPrecision(18, 4);
        builder.Property(x => x.ProductQuantity).HasPrecision(12, 3);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128);
        builder.HasIndex(x => x.DocumentNumber).IsUnique();
        builder.HasIndex(x => new { x.BranchId, x.IdempotencyKey }).IsUnique()
            .HasFilter("\"idempotency_key\" IS NOT NULL");
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PartnerProfile).WithMany().HasForeignKey(x => x.PartnerProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
    }
}
