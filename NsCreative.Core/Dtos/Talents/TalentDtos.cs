using NsCreative.Core.Enums;

namespace NsCreative.Core.Dtos.Talents;

public record TalentDto(
    Guid Id,
    string Nama,
    string Jenis,
    string? ContactPerson,
    string? Telepon,
    decimal FeeStandar,
    string? RiderDefault,
    int JumlahEvent,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record TalentListDto(
    Guid Id,
    string Nama,
    string Jenis,
    string? ContactPerson,
    string? Telepon,
    decimal FeeStandar,
    string? RiderDefault,
    int JumlahEvent);

/// <summary>Pilihan dropdown saat menugaskan talent ke event. FeeStandar jadi nilai awal fee deal.</summary>
public record TalentLookupDto(Guid Id, string Nama, string Jenis, decimal FeeStandar, string? RiderDefault);

/// <summary>Riwayat tampil talent: satu baris per event beserta fee yang disepakati.</summary>
public record TalentEventDto(
    Guid EventTalentId,
    Guid EventId,
    string NamaEvent,
    DateOnly TanggalMulai,
    DateOnly? TanggalSelesai,
    EventStatus StatusEvent,
    decimal FeeDeal,
    DateTime? JadwalTampil,
    int? DurasiMenit,
    TalentStatus Status);

/// <summary>Talent yang sama sudah dijadwalkan di event lain pada tanggal yang beririsan. Hanya peringatan.</summary>
public record TalentBentrokDto(
    Guid EventId,
    string EventNama,
    DateOnly TanggalMulai,
    DateOnly? TanggalSelesai,
    EventStatus StatusEvent,
    DateTime? JadwalTampil,
    TalentStatus Status);

public interface ITalentInput
{
    string Nama { get; }
    string Jenis { get; }
    string? ContactPerson { get; }
    string? Telepon { get; }
    decimal FeeStandar { get; }
    string? RiderDefault { get; }
}

public record CreateTalentDto(
    string Nama,
    string Jenis,
    string? ContactPerson,
    string? Telepon,
    decimal FeeStandar,
    string? RiderDefault) : ITalentInput;

public record UpdateTalentDto(
    string Nama,
    string Jenis,
    string? ContactPerson,
    string? Telepon,
    decimal FeeStandar,
    string? RiderDefault) : ITalentInput;
