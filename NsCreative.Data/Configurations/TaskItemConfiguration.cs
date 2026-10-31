using NsCreative.Core.Entities;
using NsCreative.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NsCreative.Data.Configurations;

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ConfigureBase("tasks");
        builder.Property(e => e.NamaTugas).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Deskripsi); // text
        builder.Property(e => e.Catatan); // text
        builder.Property(e => e.Status).HasDefaultValue(TaskItemStatus.Belum);

        builder.HasOne(e => e.Event)
            .WithMany(ev => ev.Tasks)
            .HasForeignKey(e => e.EventId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_tasks_event");
        // Crew PIC dihapus permanen → task tetap ada tanpa PIC (crew biasanya hanya di-soft delete).
        builder.HasOne(e => e.PicCrew)
            .WithMany()
            .HasForeignKey(e => e.PicCrewId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_tasks_pic");

        builder.HasIndex(e => e.EventId).HasDatabaseName("ix_tasks_event_id");
        builder.HasIndex(e => e.PicCrewId).HasDatabaseName("ix_tasks_pic_crew_id");
        builder.HasIndex(e => e.Status).HasDatabaseName("ix_tasks_status");
        // Dipakai dashboard: task mendekati deadline.
        builder.HasIndex(e => e.Deadline).HasDatabaseName("ix_tasks_deadline").HasFilter("((status)::text <> 'selesai'::text)");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_tasks_status", "status IN ('belum', 'dikerjakan', 'selesai', 'terlambat')"));
    }
}
