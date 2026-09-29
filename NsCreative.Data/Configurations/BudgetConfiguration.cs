using NsCreative.Core.Entities;
using NsCreative.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NsCreative.Data.Configurations;

public class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ConfigureBase("budgets");
        builder.Property(e => e.NamaKebutuhan).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Kategori).HasMaxLength(50).IsRequired();
        builder.Property(e => e.EstimasiBiaya).HasDefaultValueSql("0");
        builder.Property(e => e.BiayaAktual).HasDefaultValueSql("0");
        builder.Property(e => e.Sponsorship).HasDefaultValueSql("0");
        builder.Property(e => e.StatusPembayaran).HasDefaultValue(StatusPembayaran.BelumBayar);
        builder.Property(e => e.SumberTipe).HasMaxLength(30);

        builder.HasOne(e => e.Event)
            .WithMany(ev => ev.Budgets)
            .HasForeignKey(e => e.EventId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_budgets_event");

        builder.HasIndex(e => e.EventId).HasDatabaseName("ix_budgets_event_id");
        builder.HasIndex(e => new { e.EventId, e.Kategori }).HasDatabaseName("ix_budgets_kategori");
        // Relasi polimorfik (bukan foreign key): dicari lewat pasangan tipe + id.
        builder.HasIndex(e => new { e.SumberTipe, e.SumberId }, "ix_budgets_sumber").HasDatabaseName("ix_budgets_sumber")
            .HasFilter("(sumber_tipe IS NOT NULL)");
        // Satu baris budget per sumber, mencegah budget ganda dari endpoint transaksional.
        builder.HasIndex(e => new { e.SumberTipe, e.SumberId }, "uq_budgets_sumber").HasDatabaseName("uq_budgets_sumber")
            .IsUnique()
            .HasFilter("(sumber_tipe IS NOT NULL)");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_budgets_status",
                "status_pembayaran IN ('belum_bayar', 'dp', 'lunas')");
            t.HasCheckConstraint("ck_budgets_nilai",
                "estimasi_biaya >= 0 AND biaya_aktual >= 0 AND sponsorship >= 0");
            t.HasCheckConstraint("ck_budgets_sumber",
                "(sumber_tipe IS NULL AND sumber_id IS NULL) OR " +
                $"(sumber_tipe IS NOT NULL AND sumber_id IS NOT NULL AND sumber_tipe IN ('{Budget.SumberEventTalent}', '{Budget.SumberEventVendor}'))");
        });
    }
}
