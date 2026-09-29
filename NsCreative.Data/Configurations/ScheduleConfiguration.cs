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
        // PIC dihapus → sesi tetap ada tanpa PIC.
        builder.HasOne(e => e.PicUser)
            .WithMany()
            .HasForeignKey(e => e.PicUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_schedules_pic");

        builder.HasIndex(e => new { e.EventId, e.Tanggal, e.WaktuMulai }).HasDatabaseName("ix_schedules_event_tanggal");
        builder.HasIndex(e => e.PicUserId).HasDatabaseName("ix_schedules_pic_user_id");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_schedules_waktu", "waktu_selesai IS NULL OR waktu_selesai > waktu_mulai"));
    }
}
