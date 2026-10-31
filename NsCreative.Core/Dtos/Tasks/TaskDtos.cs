using NsCreative.Core.Enums;

namespace NsCreative.Core.Dtos.Tasks;

/// <summary>Task persiapan event.</summary>
/// <param name="Status">Status tersimpan: belum, dikerjakan, atau selesai.</param>
/// <param name="IsTerlambat">Dihitung saat baca: deadline sebelum hari ini (WIB) dan status belum selesai.</param>
/// <param name="PicCrewId">Crew penanggung jawab, harus crew yang terlibat di event.</param>
/// <param name="BolehKelola">
/// Pengguna login boleh mengubah dan menghapus task ini (admin atau PIC event). Jika false, ia
/// hanya boleh mengubah status bila task ini miliknya (<c>picCrewId</c> = crewId pengguna).
/// </param>
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
    Guid? PicCrewId,
    string? NamaPic,
    bool BolehKelola,
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

/// <summary>Filter task lintas event (<c>GET /api/tasks</c>). MilikSaya = task yang PIC-nya crew milik pengguna login.</summary>
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
    public Guid? PicCrewId { get; set; }
}

public interface ITaskInput
{
    string NamaTugas { get; }
    string? Deskripsi { get; }
    DateOnly? Deadline { get; }
    string? Catatan { get; }
    Guid? PicCrewId { get; }
}

/// <summary>Status awal selalu belum.</summary>
public record CreateTaskDto(
    string NamaTugas,
    string? Deskripsi,
    DateOnly? Deadline,
    string? Catatan,
    Guid? PicCrewId) : ITaskInput;

/// <summary>Status tidak bisa diubah lewat sini; pakai PATCH status.</summary>
public record UpdateTaskDto(
    string NamaTugas,
    string? Deskripsi,
    DateOnly? Deadline,
    string? Catatan,
    Guid? PicCrewId) : ITaskInput;

/// <summary>Hanya belum, dikerjakan, atau selesai. Terlambat dihitung otomatis.</summary>
public record UpdateTaskStatusDto(TaskItemStatus? Status);
