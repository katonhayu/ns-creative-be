using NsCreative.Core.Enums;

namespace NsCreative.Core.Dtos.Budgets;

/// <summary>Satu pos anggaran event.</summary>
/// <param name="Selisih">EstimasiBiaya - BiayaAktual (tidak disimpan). Negatif berarti melebihi estimasi.</param>
/// <param name="SumberTipe">event_talent, event_vendor, atau null untuk pos manual.</param>
/// <param name="IsTerkunci">
/// Pos otomatis dari talent/vendor: tidak bisa diubah (PUT) atau dihapus, hanya realisasinya
/// (PATCH realisasi). Estimasinya mengikuti fee/biaya di tab Talent/Vendor.
/// </param>
public record BudgetDto(
    Guid Id,
    Guid EventId,
    string NamaKebutuhan,
    string Kategori,
    decimal EstimasiBiaya,
    decimal BiayaAktual,
    decimal Sponsorship,
    decimal Selisih,
    StatusPembayaran StatusPembayaran,
    string? SumberTipe,
    Guid? SumberId,
    bool IsTerkunci,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Filter tambahan daftar budget. Kategori harus sama persis (tidak peka huruf besar/kecil).</summary>
public class BudgetFilter
{
    public string? Kategori { get; set; }
    public StatusPembayaran? StatusPembayaran { get; set; }
}

public record BudgetSummaryDto(IReadOnlyList<BudgetKategoriDto> PerKategori, BudgetGrandTotalDto GrandTotal);

public record BudgetKategoriDto(string Kategori, decimal Estimasi, decimal Aktual, decimal Selisih);

/// <param name="NetPengeluaran">Aktual - sponsorship.</param>
public record BudgetGrandTotalDto(
    decimal Estimasi,
    decimal Aktual,
    decimal Selisih,
    decimal Sponsorship,
    decimal NetPengeluaran);

public interface IBudgetInput
{
    string NamaKebutuhan { get; }
    string Kategori { get; }
    decimal EstimasiBiaya { get; }
    decimal BiayaAktual { get; }
    decimal Sponsorship { get; }
    StatusPembayaran? StatusPembayaran { get; }
}

/// <summary>Hanya pos manual; SumberTipe selalu null. Nilai yang tidak dikirim dianggap 0 / belum_bayar.</summary>
public record CreateBudgetDto(
    string NamaKebutuhan,
    string Kategori,
    decimal EstimasiBiaya,
    decimal BiayaAktual,
    decimal Sponsorship,
    StatusPembayaran? StatusPembayaran) : IBudgetInput;

/// <summary>Hanya pos manual. Pos otomatis ditolak 422.</summary>
public record UpdateBudgetDto(
    string NamaKebutuhan,
    string Kategori,
    decimal EstimasiBiaya,
    decimal BiayaAktual,
    decimal Sponsorship,
    StatusPembayaran? StatusPembayaran) : IBudgetInput;

/// <summary>Catat realisasi, termasuk untuk pos otomatis.</summary>
/// <param name="Sponsorship">Opsional; jika tidak dikirim, nilainya tidak berubah.</param>
public record UpdateRealisasiDto(decimal? BiayaAktual, StatusPembayaran? StatusPembayaran, decimal? Sponsorship);
