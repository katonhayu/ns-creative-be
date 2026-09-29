using NsCreative.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NsCreative.Data.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ConfigureBase("documents");
        builder.Property(e => e.NamaDokumen).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Jenis).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Tanggal).HasDefaultValueSql("CURRENT_DATE");
        builder.Property(e => e.FilePath).HasMaxLength(255).IsRequired();
        builder.Property(e => e.UkuranByte).HasDefaultValueSql("0");
        builder.Property(e => e.ContentType).HasMaxLength(100);
        builder.Property(e => e.Keterangan); // text

        builder.HasOne(e => e.Event)
            .WithMany(ev => ev.Documents)
            .HasForeignKey(e => e.EventId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_documents_event");

        builder.HasIndex(e => e.EventId).HasDatabaseName("ix_documents_event_id");
        builder.HasIndex(e => new { e.EventId, e.Jenis }).HasDatabaseName("ix_documents_jenis");

        builder.ToTable(t => t.HasCheckConstraint("ck_documents_ukuran", "ukuran_byte >= 0"));
    }
}
