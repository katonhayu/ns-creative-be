using NsCreative.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NsCreative.Data.Configurations;

public class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
        builder.ConfigureBase("vendors");
        builder.ConfigureSoftDelete();
        builder.Property(e => e.Nama).HasMaxLength(100).IsRequired();
        builder.Property(e => e.JenisJasa).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ContactPerson).HasMaxLength(100);
        builder.Property(e => e.Telepon).HasMaxLength(20);

        builder.HasIndex(e => e.Nama).HasDatabaseName("ix_vendors_nama").HasFilter("(deleted_at IS NULL)");
        builder.HasIndex(e => e.JenisJasa).HasDatabaseName("ix_vendors_jenis_jasa").HasFilter("(deleted_at IS NULL)");
    }
}
