using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cartex.Persistence.Configurations;

public sealed class TradeCaseConfiguration : IEntityTypeConfiguration<TradeCase>
{
    public void Configure(EntityTypeBuilder<TradeCase> builder)
    {
        builder.ToTable("trade_cases");
        builder.Property(x => x.CaseNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SiteAddress).HasMaxLength(300);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.Workflow).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.PricePolicy).HasConversion<string>().HasMaxLength(25);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128);
        builder.HasIndex(x => x.CaseNumber).IsUnique();
        builder.HasIndex(x => new { x.BranchId, x.IdempotencyKey }).IsUnique()
            .HasFilter("\"idempotency_key\" IS NOT NULL");
        builder.HasIndex(x => new { x.CustomerId, x.Status, x.BusinessDate });
        builder.HasIndex(x => new { x.BranchId, x.Status, x.Id });
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GoodsIssueDocumentConfiguration : IEntityTypeConfiguration<GoodsIssueDocument>
{
    public void Configure(EntityTypeBuilder<GoodsIssueDocument> builder)
    {
        builder.ToTable("goods_issue_documents");
        ConfigureDocument(builder);
        builder.Property(x => x.EstimatedAmount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.HasIndex(x => new { x.TradeCaseId, x.BusinessDate, x.Id });
        builder.HasOne(x => x.TradeCase).WithMany(x => x.Issues).HasForeignKey(x => x.TradeCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureDocument(EntityTypeBuilder<GoodsIssueDocument> builder)
    {
        builder.Property(x => x.DocumentNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128);
        builder.HasIndex(x => x.DocumentNumber).IsUnique();
        builder.HasIndex(x => new { x.BranchId, x.IdempotencyKey }).IsUnique()
            .HasFilter("\"idempotency_key\" IS NOT NULL");
    }
}

public sealed class GoodsIssueLineConfiguration : IEntityTypeConfiguration<GoodsIssueLine>
{
    public void Configure(EntityTypeBuilder<GoodsIssueLine> builder)
    {
        builder.ToTable("goods_issue_lines", t =>
        {
            t.HasCheckConstraint("ck_goods_issue_lines_quantity", "\"quantity\" > 0");
            t.HasCheckConstraint("ck_goods_issue_lines_balances", "\"returned_quantity\" >= 0 AND \"settled_quantity\" >= 0 AND \"returned_quantity\" + \"settled_quantity\" <= \"quantity\"");
        });
        builder.Property(x => x.Quantity).HasPrecision(12, 3);
        builder.Property(x => x.ReturnedQuantity).HasPrecision(12, 3);
        builder.Property(x => x.SettledQuantity).HasPrecision(12, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.PriceCurrency).HasMaxLength(3);
        builder.Property(x => x.PriceRate).HasPrecision(18, 6);
        builder.Property(x => x.PurchasePrice).HasPrecision(18, 2);
        builder.HasOne(x => x.Document).WithMany(x => x.Lines).HasForeignKey(x => x.GoodsIssueDocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Stock).WithMany().HasForeignKey(x => x.StockId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.GoodsIssueDocumentId, x.VariantId });
    }
}

public sealed class GoodsReturnDocumentConfiguration : IEntityTypeConfiguration<GoodsReturnDocument>
{
    public void Configure(EntityTypeBuilder<GoodsReturnDocument> builder)
    {
        builder.ToTable("goods_return_documents");
        builder.Property(x => x.DocumentNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128);
        builder.HasIndex(x => x.DocumentNumber).IsUnique();
        builder.HasIndex(x => new { x.BranchId, x.IdempotencyKey }).IsUnique()
            .HasFilter("\"idempotency_key\" IS NOT NULL");
        builder.HasIndex(x => new { x.TradeCaseId, x.BusinessDate, x.Id });
        builder.HasOne(x => x.TradeCase).WithMany(x => x.Returns).HasForeignKey(x => x.TradeCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GoodsReturnLineConfiguration : IEntityTypeConfiguration<GoodsReturnLine>
{
    public void Configure(EntityTypeBuilder<GoodsReturnLine> builder)
    {
        builder.ToTable("goods_return_lines", t =>
            t.HasCheckConstraint("ck_goods_return_lines_quantity", "\"quantity\" > 0"));
        builder.Property(x => x.Quantity).HasPrecision(12, 3);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.Condition).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.Disposition).HasConversion<string>().HasMaxLength(20);
        builder.HasOne(x => x.Document).WithMany(x => x.Lines).HasForeignKey(x => x.GoodsReturnDocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.IssueLine).WithMany().HasForeignKey(x => x.GoodsIssueLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.GoodsIssueLineId);
    }
}

public sealed class TradeCaseSettlementConfiguration : IEntityTypeConfiguration<TradeCaseSettlement>
{
    public void Configure(EntityTypeBuilder<TradeCaseSettlement> builder)
    {
        builder.ToTable("trade_case_settlements");
        builder.Property(x => x.DocumentNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128);
        builder.HasIndex(x => x.DocumentNumber).IsUnique();
        builder.HasIndex(x => x.SaleId).IsUnique();
        builder.HasIndex(x => new { x.BranchId, x.IdempotencyKey }).IsUnique()
            .HasFilter("\"idempotency_key\" IS NOT NULL");
        builder.HasOne(x => x.TradeCase).WithMany(x => x.Settlements).HasForeignKey(x => x.TradeCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Sale).WithMany().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
