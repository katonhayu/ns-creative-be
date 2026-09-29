namespace NsCreative.Core.Entities;

public class Document : BaseEntity
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public string NamaDokumen { get; set; } = "";
    public string Jenis { get; set; } = "";
    public DateOnly Tanggal { get; set; }
    /// <summary>Lokasi file di App_Data/uploads/{eventId}/, dengan nama file berupa Guid.</summary>
    public string FilePath { get; set; } = "";
    public long UkuranByte { get; set; }
    public string? ContentType { get; set; }
    public string? Keterangan { get; set; }
}
