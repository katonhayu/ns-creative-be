using NsCreative.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NsCreative.Data.Configurations;

/// <summary>
/// Kolom yang sama di semua tabel, mengikuti schema-nscreative.sql:
/// <c>id uuid PRIMARY KEY DEFAULT gen_random_uuid()</c>,
/// <c>created_at/updated_at timestamptz NOT NULL DEFAULT now()</c>,
/// dan trigger <c>trg_{tabel}_updated_at</c> yang mengisi updated_at saat update.
/// </summary>
internal static class BaseEntityConfiguration
{
    public static void ConfigureBase<T>(this EntityTypeBuilder<T> builder, string tabel) where T : BaseEntity
    {
        // Trigger dari schema-nscreative.sql; dicatat di model agar EF tahu updated_at diubah database.
        builder.ToTable(tabel, t => t.HasTrigger($"trg_{tabel}_updated_at"));
        builder.HasKey(e => e.Id).HasName($"{tabel}_pkey");

        // Default gen_random_uuid() hanya untuk insert manual lewat DBeaver.
        // Aplikasi selalu mengirim Id sendiri (UUIDv7, bagian 4.1 CLAUDE.md).
        builder.Property(e => e.Id)
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedNever();

        // Diisi database, bukan aplikasi: EF tidak mengirim kolom ini dan membacanya kembali.
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();
    }

    /// <summary>Soft delete: baris dengan DeletedAt terisi tidak ikut terbaca di query biasa.</summary>
    public static void ConfigureSoftDelete<T>(this EntityTypeBuilder<T> builder) where T : BaseEntity, ISoftDelete
    {
        builder.HasQueryFilter(e => e.DeletedAt == null);
    }
}
