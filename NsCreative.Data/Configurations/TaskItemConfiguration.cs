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
        // PIC dihapus → task tetap ada tanpa PIC.
        builder.HasOne(e => e.PicUser)
            .WithMany()
            .HasForeignKey(e => e.PicUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_tasks_pic");

        builder.HasIndex(e => e.EventId).HasDatabaseName("ix_tasks_event_id");
        builder.HasIndex(e => e.PicUserId).HasDatabaseName("ix_tasks_pic_user_id");
        builder.HasIndex(e => e.Status).HasDatabaseName("ix_tasks_status");
        // Dipakai dashboard: task mendekati deadline.
        builder.HasIndex(e => e.Deadline).HasDatabaseName("ix_tasks_deadline").HasFilter("((status)::text <> 'selesai'::text)");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_tasks_status", "status IN ('belum', 'dikerjakan', 'selesai', 'terlambat')"));
    }
}
