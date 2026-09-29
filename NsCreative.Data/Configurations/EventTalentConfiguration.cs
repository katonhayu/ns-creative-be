using NsCreative.Core.Entities;
using NsCreative.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NsCreative.Data.Configurations;

public class EventTalentConfiguration : IEntityTypeConfiguration<EventTalent>
{
    public void Configure(EntityTypeBuilder<EventTalent> builder)
    {
        builder.ConfigureBase("event_talents");
        builder.Property(e => e.FeeDeal).HasDefaultValueSql("0");
        builder.Property(e => e.JadwalTampil); // timestamptz
        builder.Property(e => e.Rider); // text
        builder.Property(e => e.Status).HasDefaultValue(TalentStatus.Dihubungi);

        builder.HasOne(e => e.Event)
            .WithMany(ev => ev.Talents)
            .HasForeignKey(e => e.EventId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_event_talents_event");
        builder.HasOne(e => e.Talent)
            .WithMany()
            .HasForeignKey(e => e.TalentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_event_talents_talent");

        builder.HasIndex(e => new { e.EventId, e.TalentId }).HasDatabaseName("uq_event_talents").IsUnique();
        builder.HasIndex(e => e.EventId).HasDatabaseName("ix_event_talents_event_id");
        builder.HasIndex(e => e.TalentId).HasDatabaseName("ix_event_talents_talent_id");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_event_talents_status",
                "status IN ('dihubungi', 'deal', 'kontrak', 'selesai', 'batal')");
            t.HasCheckConstraint("ck_event_talents_fee", "fee_deal >= 0");
            t.HasCheckConstraint("ck_event_talents_durasi", "durasi_menit IS NULL OR durasi_menit > 0");
        });
    }
}
