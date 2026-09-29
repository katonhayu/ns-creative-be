namespace NsCreative.Core.Entities;

/// <summary>Satu sesi rundown event.</summary>
public class Schedule : BaseEntity
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid? PicUserId { get; set; }
    public User? PicUser { get; set; }

    public DateOnly Tanggal { get; set; }
    public TimeOnly WaktuMulai { get; set; }
    public TimeOnly? WaktuSelesai { get; set; }
    public string Kegiatan { get; set; } = "";
    public string? Lokasi { get; set; }
}
