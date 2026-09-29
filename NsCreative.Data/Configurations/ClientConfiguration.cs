using NsCreative.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NsCreative.Data.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ConfigureBase("clients");
        builder.ConfigureSoftDelete();
        builder.Property(e => e.Nama).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Perusahaan).HasMaxLength(150);
        builder.Property(e => e.Telepon).HasMaxLength(20);
        builder.Property(e => e.Email).HasMaxLength(100);
        builder.Property(e => e.Alamat); // text

        builder.HasIndex(e => e.Nama).HasDatabaseName("ix_clients_nama").HasFilter("(deleted_at IS NULL)");
        builder.HasIndex(e => e.Perusahaan).HasDatabaseName("ix_clients_perusahaan").HasFilter("(deleted_at IS NULL)");
    }
}
