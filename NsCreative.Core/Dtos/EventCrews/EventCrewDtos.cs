using NsCreative.Core.Dtos.Crews;

namespace NsCreative.Core.Dtos.EventCrews;

/// <summary>Penugasan crew di satu event. Tidak punya data finansial.</summary>
/// <param name="Bentrok">
/// Penugasan crew yang sama di event lain pada tanggal yang beririsan. Hanya peringatan.
/// Tanggal yang dicek: tanggal bertugas jika diisi, selain itu seluruh rentang event.
/// </param>
public record EventCrewDto(
    Guid Id,
    Guid EventId,
    Guid CrewId,
    string NamaCrew,
    string? Keahlian,
    string? Telepon,
    string Posisi,
    DateOnly? TanggalBertugas,
    IReadOnlyList<CrewBentrokDto> Bentrok,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Pilihan PIC task dan sesi rundown di satu event.</summary>
/// <param name="Keterangan">"PIC event", atau posisi crew di event ini.</param>
/// <param name="PunyaAkun">Crew punya akun login, jadi bisa melihat task-nya dan mengubah statusnya sendiri.</param>
public record PenanggungJawabDto(Guid CrewId, string Nama, string Keterangan, bool PunyaAkun);

public interface IEventCrewInput
{
    string Posisi { get; }
    DateOnly? TanggalBertugas { get; }
}

public record CreateEventCrewDto(Guid CrewId, string Posisi, DateOnly? TanggalBertugas) : IEventCrewInput;

/// <summary>Crew tidak bisa diganti; hapus lalu tugaskan ulang.</summary>
public record UpdateEventCrewDto(string Posisi, DateOnly? TanggalBertugas) : IEventCrewInput;
