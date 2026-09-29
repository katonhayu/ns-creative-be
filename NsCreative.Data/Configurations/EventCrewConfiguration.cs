using NsCreative.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NsCreative.Data.Configurations;

public class EventCrewConfiguration : IEntityTypeConfiguration<EventCrew>
{
    public void Configure(EntityTypeBuilder<EventCrew> builder)
    {
        builder.ConfigureBase("event_crews");
        builder.Property(e => e.Posisi).HasMaxLength(100).IsRequired();

        builder.HasOne(e => e.Event)
            .WithMany(ev => ev.Crews)
            .HasForeignKey(e => e.EventId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_event_crews_event");
        builder.HasOne(e => e.Crew)
            .WithMany()
            .HasForeignKey(e => e.CrewId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_event_crews_crew");

        builder.HasIndex(e => new { e.EventId, e.CrewId }).HasDatabaseName("uq_event_crews").IsUnique();
        builder.HasIndex(e => e.EventId).HasDatabaseName("ix_event_crews_event_id");
        builder.HasIndex(e => e.CrewId).HasDatabaseName("ix_event_crews_crew_id");
        // Dipakai endpoint cek bentrok jadwal.
        builder.HasIndex(e => new { e.CrewId, e.TanggalBertugas }).HasDatabaseName("ix_event_crews_crew_tanggal");
    }
}
