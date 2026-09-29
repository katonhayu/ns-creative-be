using NsCreative.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NsCreative.Data.Configurations;

public class CrewConfiguration : IEntityTypeConfiguration<Crew>
{
    public void Configure(EntityTypeBuilder<Crew> builder)
    {
        builder.ConfigureBase("crews");
        builder.ConfigureSoftDelete();
        builder.Property(e => e.Nama).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Telepon).HasMaxLength(20);
        builder.Property(e => e.Keahlian); // text

        // Akun login dihapus → crew tetap ada, tautannya dilepas.
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_crews_user");

        builder.HasIndex(e => e.Nama).HasDatabaseName("ix_crews_nama").HasFilter("(deleted_at IS NULL)");
        builder.HasIndex(e => e.UserId).HasDatabaseName("ix_crews_user_id");
    }
}
