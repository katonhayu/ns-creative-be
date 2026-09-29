using NsCreative.Core.Enums;

namespace NsCreative.Core.Dtos.EventTalents;

/// <summary>Penugasan talent di satu event beserta baris budget yang dibuat otomatis.</summary>
/// <param name="JadwalTampil">UTC. Frontend menampilkannya dalam WIB.</param>
/// <param name="BudgetId">Baris budget terkait (kategori Talent). Null hanya jika datanya tidak konsisten.</param>
/// <param name="BiayaAktual">Realisasi di budget terkait. Penugasan tidak bisa dihapus jika lebih dari 0.</param>
public record EventTalentDto(
    Guid Id,
    Guid EventId,
    Guid TalentId,
    string NamaTalent,
    string JenisTalent,
    decimal FeeStandar,
    decimal FeeDeal,
    int? DurasiMenit,
    DateTime? JadwalTampil,
    string? Rider,
    TalentStatus Status,
    Guid? BudgetId,
    decimal BiayaAktual,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public interface IEventTalentInput
{
    decimal FeeDeal { get; }
    int? DurasiMenit { get; }
    /// <summary>Tanpa zona waktu dianggap WIB, misalnya "2026-12-20T20:00:00".</summary>
    DateTime? JadwalTampil { get; }
    string? Rider { get; }
}

/// <summary>Status opsional, default <c>dihubungi</c>.</summary>
public record CreateEventTalentDto(
    Guid TalentId,
    decimal FeeDeal,
    int? DurasiMenit,
    DateTime? JadwalTampil,
    string? Rider,
    TalentStatus? Status) : IEventTalentInput;

/// <summary>Talent tidak bisa diganti; hapus lalu tugaskan ulang. Status lewat PATCH status.</summary>
public record UpdateEventTalentDto(
    decimal FeeDeal,
    int? DurasiMenit,
    DateTime? JadwalTampil,
    string? Rider) : IEventTalentInput;

public record UpdateEventTalentStatusDto(TalentStatus? Status);
