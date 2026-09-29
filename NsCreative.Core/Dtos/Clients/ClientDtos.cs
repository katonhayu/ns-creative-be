using NsCreative.Core.Enums;

namespace NsCreative.Core.Dtos.Clients;

/// <summary>Detail satu client.</summary>
public record ClientDto(
    Guid Id,
    string Nama,
    string? Perusahaan,
    string? Telepon,
    string? Email,
    string? Alamat,
    int JumlahEvent,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Baris daftar client.</summary>
public record ClientListDto(
    Guid Id,
    string Nama,
    string? Perusahaan,
    string? Telepon,
    string? Email,
    string? Alamat,
    int JumlahEvent);

/// <summary>Pilihan dropdown client.</summary>
public record ClientLookupDto(Guid Id, string Nama, string? Perusahaan);

/// <summary>Satu baris riwayat event milik client.</summary>
public record ClientEventDto(
    Guid Id,
    string Nama,
    string Jenis,
    DateOnly TanggalMulai,
    DateOnly? TanggalSelesai,
    string? Lokasi,
    EventStatus Status);

/// <summary>Field yang sama pada create dan update, untuk validator bersama.</summary>
public interface IClientInput
{
    string Nama { get; }
    string? Perusahaan { get; }
    string? Telepon { get; }
    string? Email { get; }
    string? Alamat { get; }
}

public record CreateClientDto(
    string Nama,
    string? Perusahaan,
    string? Telepon,
    string? Email,
    string? Alamat) : IClientInput;

public record UpdateClientDto(
    string Nama,
    string? Perusahaan,
    string? Telepon,
    string? Email,
    string? Alamat) : IClientInput;
