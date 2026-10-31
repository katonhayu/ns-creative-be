namespace NsCreative.Core.Entities;

/// <summary>Satu sesi rundown event.</summary>
public class Schedule : BaseEntity
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    /// <summary>Crew yang terlibat di event: PIC event atau crew yang ditugaskan.</summary>
    public Guid? PicCrewId { get; set; }
    public Crew? PicCrew { get; set; }

    public DateOnly Tanggal { get; set; }
    public TimeOnly WaktuMulai { get; set; }
    public TimeOnly? WaktuSelesai { get; set; }
    public string Kegiatan { get; set; } = "";
    public string? Lokasi { get; set; }
}
