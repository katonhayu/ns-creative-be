using NsCreative.Core.Enums;

namespace NsCreative.Core.Dtos.Crews;

public record CrewDto(
    Guid Id,
    Guid? UserId,
    string? EmailAkun,
    string Nama,
    string? Telepon,
    string? Keahlian,
    int JumlahEvent,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CrewListDto(
    Guid Id,
    Guid? UserId,
    string? EmailAkun,
    string Nama,
    string? Telepon,
    string? Keahlian,
    int JumlahEvent);

/// <param name="PunyaAkun">Ditautkan ke akun login aktif, sehingga bisa menjadi PIC event.</param>
public record CrewLookupDto(Guid Id, string Nama, string? Keahlian, bool PunyaAkun);

/// <summary>Riwayat penugasan crew di satu event.</summary>
public record CrewEventDto(
    Guid EventCrewId,
    Guid EventId,
    string NamaEvent,
    DateOnly TanggalMulai,
    DateOnly? TanggalSelesai,
    EventStatus StatusEvent,
    string Posisi,
    DateOnly? TanggalBertugas);

/// <summary>Penugasan crew di event lain yang tanggalnya beririsan. Hanya peringatan.</summary>
public record CrewBentrokDto(
    Guid EventId,
    string EventNama,
    DateOnly TanggalMulai,
    DateOnly? TanggalSelesai,
    EventStatus StatusEvent,
    string Posisi,
    DateOnly? TanggalBertugas);

public interface ICrewInput
{
    /// <summary>Akun login (peran crew) yang ditautkan. Kosong jika crew tidak perlu login.</summary>
    Guid? UserId { get; }
    string Nama { get; }
    string? Telepon { get; }
    string? Keahlian { get; }
}

public record CreateCrewDto(Guid? UserId, string Nama, string? Telepon, string? Keahlian) : ICrewInput;

public record UpdateCrewDto(Guid? UserId, string Nama, string? Telepon, string? Keahlian) : ICrewInput;
