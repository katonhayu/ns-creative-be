namespace NsCreative.Core.Dtos.Schedules;

/// <summary>Satu sesi rundown. Waktu berformat "20:00:00".</summary>
/// <param name="DiLuarRentangEvent">Tanggal sesi di luar tanggal event. Hanya peringatan.</param>
public record ScheduleDto(
    Guid Id,
    Guid EventId,
    string NamaEvent,
    DateOnly Tanggal,
    TimeOnly WaktuMulai,
    TimeOnly? WaktuSelesai,
    string Kegiatan,
    string? Lokasi,
    Guid? PicUserId,
    string? NamaPic,
    bool DiLuarRentangEvent,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Filter jadwal lintas event (<c>GET /api/schedules</c>). Semua opsional.</summary>
public class JadwalFilter
{
    public Guid? EventId { get; set; }
    public DateOnly? DariTanggal { get; set; }
    public DateOnly? SampaiTanggal { get; set; }
}

/// <summary>Rundown satu hari, sesi diurutkan waktu mulai.</summary>
public record RundownHariDto(DateOnly Tanggal, bool DiLuarRentangEvent, IReadOnlyList<ScheduleDto> Items);

/// <summary>Tanggal dan waktu mulai nullable hanya supaya "tidak diisi" bisa dibedakan dari 00:00.</summary>
public interface IScheduleInput
{
    DateOnly? Tanggal { get; }
    TimeOnly? WaktuMulai { get; }
    TimeOnly? WaktuSelesai { get; }
    string Kegiatan { get; }
    string? Lokasi { get; }
    Guid? PicUserId { get; }
}

public record CreateScheduleDto(
    DateOnly? Tanggal,
    TimeOnly? WaktuMulai,
    TimeOnly? WaktuSelesai,
    string Kegiatan,
    string? Lokasi,
    Guid? PicUserId) : IScheduleInput;

public record UpdateScheduleDto(
    DateOnly? Tanggal,
    TimeOnly? WaktuMulai,
    TimeOnly? WaktuSelesai,
    string Kegiatan,
    string? Lokasi,
    Guid? PicUserId) : IScheduleInput;
