using NsCreative.Core.Entities;
using NsCreative.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NsCreative.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ConfigureBase("users");
        builder.Property(e => e.Nama).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(100).IsRequired();
        builder.Property(e => e.PasswordHash).HasMaxLength(255).IsRequired();

        // Sentinel = nilai default database, supaya EF tetap mengirim nilai lain apa adanya
        // (tanpa sentinel, Role = Admin (0) dan IsAktif = false akan dianggap "tidak diisi").
        builder.Property(e => e.Role).HasDefaultValue(UserRole.Crew).HasSentinel(UserRole.Crew);
        builder.Property(e => e.IsAktif).HasDefaultValue(true).HasSentinel(true);

        builder.HasIndex(e => e.Email).HasDatabaseName("uq_users_email").IsUnique();
        builder.HasIndex(e => e.Role).HasDatabaseName("ix_users_role").HasFilter("(is_aktif = true)");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_users_role", "role IN ('admin', 'event_manager', 'crew')"));
    }
}
