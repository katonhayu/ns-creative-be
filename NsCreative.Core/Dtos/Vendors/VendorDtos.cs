using NsCreative.Core.Common;
using NsCreative.Core.Enums;

namespace NsCreative.Core.Dtos.Vendors;

public record VendorDto(
    Guid Id,
    string Nama,
    string JenisJasa,
    string? ContactPerson,
    string? Telepon,
    int JumlahEvent,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record VendorListDto(
    Guid Id,
    string Nama,
    string JenisJasa,
    string? ContactPerson,
    string? Telepon,
    int JumlahEvent);

public record VendorLookupDto(Guid Id, string Nama, string JenisJasa);

/// <summary>Satu transaksi vendor di satu event.</summary>
public record VendorEventDto(
    Guid EventVendorId,
    Guid EventId,
    string NamaEvent,
    DateOnly TanggalMulai,
    DateOnly? TanggalSelesai,
    EventStatus StatusEvent,
    string? Kebutuhan,
    decimal Biaya,
    StatusPembayaran StatusPembayaran);

/// <summary>Vendor yang sama sudah dipesan di event lain pada tanggal yang beririsan. Hanya peringatan.</summary>
public record VendorBentrokDto(
    Guid EventId,
    string EventNama,
    DateOnly TanggalMulai,
    DateOnly? TanggalSelesai,
    EventStatus StatusEvent,
    string? Kebutuhan);

/// <summary>Riwayat vendor: daftar transaksi berpaginasi ditambah total dari seluruh transaksi.</summary>
public record VendorRiwayatDto(
    decimal TotalTransaksi,
    decimal TotalBelumLunas,
    int JumlahTransaksi,
    PagedResult<VendorEventDto> Riwayat);

public interface IVendorInput
{
    string Nama { get; }
    string JenisJasa { get; }
    string? ContactPerson { get; }
    string? Telepon { get; }
}

public record CreateVendorDto(string Nama, string JenisJasa, string? ContactPerson, string? Telepon) : IVendorInput;

public record UpdateVendorDto(string Nama, string JenisJasa, string? ContactPerson, string? Telepon) : IVendorInput;
