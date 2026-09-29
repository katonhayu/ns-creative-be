using NsCreative.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NsCreative.Data.Configurations;

/// <summary>
/// Tabel pertama yang dibuat lewat migration (AddRefreshTokens), bukan dari schema-nscreative.sql.
/// DDL-nya juga dicatat di file SQL tersebut supaya keduanya tetap sama.
/// </summary>
public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ConfigureBase("refresh_tokens");
        builder.Property(e => e.TokenHash).HasMaxLength(64).IsRequired();

        // Akun dihapus → semua sesinya ikut hilang.
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_refresh_tokens_user");

        builder.HasIndex(e => e.TokenHash).HasDatabaseName("uq_refresh_tokens_hash").IsUnique();
        builder.HasIndex(e => e.UserId).HasDatabaseName("ix_refresh_tokens_user_id");
    }
}
