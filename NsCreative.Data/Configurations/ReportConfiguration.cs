using NsCreative.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NsCreative.Data.Configurations;

public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ConfigureBase("reports");
        builder.Property(e => e.HasilPelaksanaan); // text
        builder.Property(e => e.TotalPengeluaran).HasDefaultValueSql("0");
        builder.Property(e => e.Kendala); // text
        builder.Property(e => e.Evaluasi); // text
        builder.Property(e => e.Catatan); // text
        builder.Property(e => e.TanggalDibuat).HasDefaultValueSql("CURRENT_DATE");

        // Satu laporan per event.
        builder.HasOne(e => e.Event)
            .WithOne(ev => ev.Report)
            .HasForeignKey<Report>(e => e.EventId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_reports_event");
        builder.HasIndex(e => e.EventId).HasDatabaseName("uq_reports_event").IsUnique();

        builder.ToTable(t => t.HasCheckConstraint("ck_reports_total", "total_pengeluaran >= 0"));
    }
}
