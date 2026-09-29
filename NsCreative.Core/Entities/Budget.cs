using NsCreative.Core.Enums;

namespace NsCreative.Core.Entities;

/// <summary>
/// Pos anggaran event. SumberTipe + SumberId adalah relasi polimorfik ke
/// event_talents atau event_vendors, bukan foreign key; null berarti pos manual.
/// Selisih tidak disimpan, dihitung di DTO: EstimasiBiaya - BiayaAktual.
/// </summary>
public class Budget : BaseEntity
{
    public const string SumberEventTalent = "event_talent";
    public const string SumberEventVendor = "event_vendor";

    /// <summary>Kategori baris yang dibuat otomatis saat talent atau vendor ditugaskan ke event.</summary>
    public const string KategoriTalent = "Talent";
    public const string KategoriVendor = "Vendor";

    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public string NamaKebutuhan { get; set; } = "";
    public string Kategori { get; set; } = "";
    public decimal EstimasiBiaya { get; set; }
    public decimal BiayaAktual { get; set; }
    public decimal Sponsorship { get; set; }
    public StatusPembayaran StatusPembayaran { get; set; } = StatusPembayaran.BelumBayar;
    public string? SumberTipe { get; set; }
    public Guid? SumberId { get; set; }
}
