using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cartex.Persistence.Configurations;

public class BusinessSettingConfiguration : IEntityTypeConfiguration<BusinessSetting>
{
    public void Configure(EntityTypeBuilder<BusinessSetting> builder)
    {
        builder.ToTable("business_settings");
        builder.Property(x => x.Key).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Key).IsUnique();
        builder.Property(x => x.Value).HasColumnType("jsonb");
    }
}
