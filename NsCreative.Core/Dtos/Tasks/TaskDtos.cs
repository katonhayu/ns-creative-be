using NsCreative.Core.Enums;

namespace NsCreative.Core.Dtos.Tasks;

/// <summary>Task persiapan event.</summary>
/// <param name="Status">Status tersimpan: belum, dikerjakan, atau selesai.</param>
/// <param name="IsTerlambat">Dihitung saat baca: deadline sebelum hari ini (WIB) dan status belum selesai.</param>
public record TaskDto(
    Guid Id,
    Guid EventId,
    string NamaEvent,
    string NamaTugas,
    string? Deskripsi,
    DateOnly? Deadline,
    TaskItemStatus Status,
    bool IsTerlambat,
    string? Catatan,
    Guid? PicUserId,
    string? NamaPic,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Task milik pengguna login lintas event (<c>GET /api/tasks/mine</c>).</summary>
public record MyTaskDto(
    Guid Id,
    Guid EventId,
    string NamaEvent,
    EventStatus StatusEvent,
    DateOnly TanggalMulaiEvent,
    string NamaTugas,
    string? Deskripsi,
    DateOnly? Deadline,
    TaskItemStatus Status,
    bool IsTerlambat,
    string? Catatan);

/// <summary>Filter task lintas event (<c>GET /api/tasks</c>). Crew selalu hanya task miliknya.</summary>
public class TaskLintasEventFilter
{
    public Guid? EventId { get; set; }
    public bool MilikSaya { get; set; }
    public TaskItemStatus? Status { get; set; }
}

/// <summary>Filter tambahan daftar task. <c>status=terlambat</c> berarti deadline lewat dan belum selesai.</summary>
public class TaskFilter
{
    public TaskItemStatus? Status { get; set; }
    public Guid? PicUserId { get; set; }
}

public interface ITaskInput
{
    string NamaTugas { get; }
    string? Deskripsi { get; }
    DateOnly? Deadline { get; }
    string? Catatan { get; }
    Guid? PicUserId { get; }
}

/// <summary>Status awal selalu belum.</summary>
public record CreateTaskDto(
    string NamaTugas,
    string? Deskripsi,
    DateOnly? Deadline,
    string? Catatan,
    Guid? PicUserId) : ITaskInput;

/// <summary>Status tidak bisa diubah lewat sini; pakai PATCH status.</summary>
public record UpdateTaskDto(
    string NamaTugas,
    string? Deskripsi,
    DateOnly? Deadline,
    string? Catatan,
    Guid? PicUserId) : ITaskInput;

/// <summary>Hanya belum, dikerjakan, atau selesai. Terlambat dihitung otomatis.</summary>
public record UpdateTaskStatusDto(TaskItemStatus? Status);
