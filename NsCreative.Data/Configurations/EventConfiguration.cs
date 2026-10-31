using NsCreative.Core.Entities;
using NsCreative.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NsCreative.Data.Configurations;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ConfigureBase("events");
        builder.Property(e => e.Nama).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Jenis).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Lokasi).HasMaxLength(200);
        builder.Property(e => e.Deskripsi); // text
        builder.Property(e => e.Status).HasDefaultValue(EventStatus.Draft);

        // Master data tidak boleh terhapus selama masih dipakai event.
        builder.HasOne(e => e.Client)
            .WithMany()
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_events_client");
        builder.HasOne(e => e.PicCrew)
            .WithMany()
            .HasForeignKey(e => e.PicCrewId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_events_pic");

        builder.HasIndex(e => e.ClientId).HasDatabaseName("ix_events_client_id");
        builder.HasIndex(e => e.PicCrewId).HasDatabaseName("ix_events_pic_crew_id");
        builder.HasIndex(e => e.Status).HasDatabaseName("ix_events_status");
        builder.HasIndex(e => e.TanggalMulai).HasDatabaseName("ix_events_tanggal_mulai").IsDescending();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_events_status",
                "status IN ('draft', 'perencanaan', 'dikonfirmasi', 'berlangsung', 'selesai', 'dibatalkan')");
            t.HasCheckConstraint("ck_events_tanggal",
                "tanggal_selesai IS NULL OR tanggal_selesai >= tanggal_mulai");
        });
    }
}
