using NsCreative.Core.Enums;

namespace NsCreative.Core.Entities;

/// <summary>Penugasan talent ke event beserta kesepakatan khusus event tersebut.</summary>
public class EventTalent : BaseEntity
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid TalentId { get; set; }
    public Talent Talent { get; set; } = null!;

    public decimal FeeDeal { get; set; }
    public int? DurasiMenit { get; set; }
    /// <summary>Jadwal tampil (timestamptz). Simpan dalam UTC; tampilkan dalam WIB di frontend.</summary>
    public DateTime? JadwalTampil { get; set; }
    public string? Rider { get; set; }
    public TalentStatus Status { get; set; } = TalentStatus.Dihubungi;
}
