namespace NsCreative.Core.Entities;

public class EventCrew : BaseEntity
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid CrewId { get; set; }
    public Crew Crew { get; set; } = null!;

    public string Posisi { get; set; } = "";
    public DateOnly? TanggalBertugas { get; set; }
}
