using NsCreative.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NsCreative.Data.Configurations;

public class TalentConfiguration : IEntityTypeConfiguration<Talent>
{
    public void Configure(EntityTypeBuilder<Talent> builder)
    {
        builder.ConfigureBase("talents");
        builder.ConfigureSoftDelete();
        builder.Property(e => e.Nama).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Jenis).HasMaxLength(50).IsRequired();
        builder.Property(e => e.ContactPerson).HasMaxLength(100);
        builder.Property(e => e.Telepon).HasMaxLength(20);
        builder.Property(e => e.FeeStandar).HasDefaultValueSql("0");
        builder.Property(e => e.RiderDefault); // text

        builder.HasIndex(e => e.Nama).HasDatabaseName("ix_talents_nama").HasFilter("(deleted_at IS NULL)");
        builder.HasIndex(e => e.Jenis).HasDatabaseName("ix_talents_jenis").HasFilter("(deleted_at IS NULL)");

        builder.ToTable(t => t.HasCheckConstraint("ck_talents_fee", "fee_standar >= 0"));
    }
}
