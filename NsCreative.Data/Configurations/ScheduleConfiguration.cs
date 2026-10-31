using NsCreative.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NsCreative.Data.Configurations;

public class ScheduleConfiguration : IEntityTypeConfiguration<Schedule>
{
    public void Configure(EntityTypeBuilder<Schedule> builder)
    {
        builder.ConfigureBase("schedules");
        builder.Property(e => e.Kegiatan).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Lokasi).HasMaxLength(200);

        builder.HasOne(e => e.Event)
            .WithMany(ev => ev.Schedules)
            .HasForeignKey(e => e.EventId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_schedules_event");
        // Crew PIC dihapus permanen → sesi tetap ada tanpa PIC (crew biasanya hanya di-soft delete).
        builder.HasOne(e => e.PicCrew)
            .WithMany()
            .HasForeignKey(e => e.PicCrewId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_schedules_pic");

        builder.HasIndex(e => new { e.EventId, e.Tanggal, e.WaktuMulai }).HasDatabaseName("ix_schedules_event_tanggal");
        builder.HasIndex(e => e.PicCrewId).HasDatabaseName("ix_schedules_pic_crew_id");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_schedules_waktu", "waktu_selesai IS NULL OR waktu_selesai > waktu_mulai"));
    }
}
