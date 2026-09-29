using NsCreative.Core.Enums;

namespace NsCreative.Core.Dtos.EventVendors;

/// <summary>Transaksi vendor di satu event beserta baris budget yang dibuat otomatis.</summary>
/// <param name="BudgetId">Baris budget terkait (kategori Vendor). Null hanya jika datanya tidak konsisten.</param>
/// <param name="BiayaAktual">Realisasi di budget terkait. Transaksi tidak bisa dihapus jika lebih dari 0.</param>
public record EventVendorDto(
    Guid Id,
    Guid EventId,
    Guid VendorId,
    string NamaVendor,
    string JenisJasa,
    string? Kebutuhan,
    decimal Biaya,
    StatusPembayaran StatusPembayaran,
    Guid? BudgetId,
    decimal BiayaAktual,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public interface IEventVendorInput
{
    string? Kebutuhan { get; }
    decimal Biaya { get; }
}

/// <summary>Satu vendor boleh punya beberapa transaksi di event yang sama. Status pembayaran opsional, default <c>belum_bayar</c>.</summary>
public record CreateEventVendorDto(
    Guid VendorId,
    string? Kebutuhan,
    decimal Biaya,
    StatusPembayaran? StatusPembayaran) : IEventVendorInput;

/// <summary>Vendor tidak bisa diganti. Status pembayaran lewat PATCH pembayaran.</summary>
public record UpdateEventVendorDto(string? Kebutuhan, decimal Biaya) : IEventVendorInput;

public record UpdateEventVendorPembayaranDto(StatusPembayaran? StatusPembayaran);
