using NsCreative.Core.Enums;

namespace NsCreative.Core.Entities;

public class Event : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;

    public Guid PicUserId { get; set; }
    public User PicUser { get; set; } = null!;

    public string Nama { get; set; } = "";
    public string Jenis { get; set; } = "";
    public DateOnly TanggalMulai { get; set; }
    public DateOnly? TanggalSelesai { get; set; }
    public string? Lokasi { get; set; }
    public string? Deskripsi { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Draft;

    public ICollection<EventTalent> Talents { get; set; } = [];
    public ICollection<EventCrew> Crews { get; set; } = [];
    public ICollection<EventVendor> Vendors { get; set; } = [];
    public ICollection<Schedule> Schedules { get; set; } = [];
    public ICollection<TaskItem> Tasks { get; set; } = [];
    public ICollection<Budget> Budgets { get; set; } = [];
    public ICollection<Document> Documents { get; set; } = [];
    public Report? Report { get; set; }
}
