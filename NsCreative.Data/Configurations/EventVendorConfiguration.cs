using NsCreative.Core.Entities;
using NsCreative.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NsCreative.Data.Configurations;

public class EventVendorConfiguration : IEntityTypeConfiguration<EventVendor>
{
    public void Configure(EntityTypeBuilder<EventVendor> builder)
    {
        builder.ConfigureBase("event_vendors");
        builder.Property(e => e.Kebutuhan); // text
        builder.Property(e => e.Biaya).HasDefaultValueSql("0");
        builder.Property(e => e.StatusPembayaran).HasDefaultValue(StatusPembayaran.BelumBayar);

        builder.HasOne(e => e.Event)
            .WithMany(ev => ev.Vendors)
            .HasForeignKey(e => e.EventId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_event_vendors_event");
        builder.HasOne(e => e.Vendor)
            .WithMany()
            .HasForeignKey(e => e.VendorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_event_vendors_vendor");

        builder.HasIndex(e => e.EventId).HasDatabaseName("ix_event_vendors_event_id");
        builder.HasIndex(e => e.VendorId).HasDatabaseName("ix_event_vendors_vendor_id");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_event_vendors_status",
                "status_pembayaran IN ('belum_bayar', 'dp', 'lunas')");
            t.HasCheckConstraint("ck_event_vendors_biaya", "biaya >= 0");
        });
    }
}
