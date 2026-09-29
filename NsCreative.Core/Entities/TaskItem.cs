using NsCreative.Core.Enums;

namespace NsCreative.Core.Entities;

/// <summary>Task event. Dinamai TaskItem agar tidak bentrok dengan System.Threading.Tasks.Task.</summary>
public class TaskItem : BaseEntity
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid? PicUserId { get; set; }
    public User? PicUser { get; set; }

    public string NamaTugas { get; set; } = "";
    public string? Deskripsi { get; set; }
    public DateOnly? Deadline { get; set; }
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Belum;
    public string? Catatan { get; set; }
}
