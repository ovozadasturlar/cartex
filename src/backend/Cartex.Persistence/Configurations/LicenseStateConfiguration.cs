using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cartex.Persistence.Configurations;

public class LicenseStateConfiguration : IEntityTypeConfiguration<LicenseState>
{
    public void Configure(EntityTypeBuilder<LicenseState> builder)
    {
        builder.ToTable("license_states", t =>
            t.HasCheckConstraint("ck_license_states_singleton", $"\"id\" = {LicenseState.SingletonId}"));
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Tariff).HasMaxLength(40).IsRequired();
        builder.Property(x => x.EnabledFeatures).HasColumnType("jsonb");
    }
}
