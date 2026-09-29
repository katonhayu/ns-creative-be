using NsCreative.Core.Enums;

namespace NsCreative.Core.Dtos.Reports;

/// <summary>Laporan evaluasi event.</summary>
/// <param name="TotalPengeluaran">Snapshot SUM(biaya aktual) saat laporan dibuat. Dibekukan, tidak ikut koreksi budget.</param>
/// <param name="TotalAktualSaatIni">SUM(biaya aktual) sekarang, pembanding jika budget dikoreksi setelah laporan dibuat.</param>
public record ReportDto(
    Guid Id,
    Guid EventId,
    string NamaEvent,
    EventStatus StatusEvent,
    DateOnly TanggalMulai,
    DateOnly? TanggalSelesai,
    string NamaClient,
    string? HasilPelaksanaan,
    decimal TotalPengeluaran,
    string? Kendala,
    string? Evaluasi,
    string? Catatan,
    DateOnly TanggalDibuat,
    decimal TotalEstimasi,
    decimal TotalSponsorship,
    decimal TotalAktualSaatIni,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record ReportListDto(
    Guid Id,
    Guid EventId,
    string NamaEvent,
    string NamaClient,
    DateOnly TanggalMulai,
    DateOnly? TanggalSelesai,
    decimal TotalPengeluaran,
    DateOnly TanggalDibuat);

/// <summary>
/// Baris halaman Laporan: event yang sudah selesai beserta laporannya jika sudah dibuat
/// (<c>GET /api/reports/events</c>).
/// </summary>
/// <param name="Laporan">Null jika laporan belum dibuat.</param>
public record LaporanEventDto(
    Guid EventId,
    string NamaEvent,
    string NamaClient,
    DateOnly TanggalMulai,
    DateOnly? TanggalSelesai,
    EventStatus StatusEvent,
    int JumlahDokumen,
    RingkasanLaporanDto? Laporan);

public record RingkasanLaporanDto(
    Guid Id,
    string? HasilPelaksanaan,
    decimal TotalPengeluaran,
    string? Kendala,
    string? Evaluasi,
    string? Catatan,
    DateOnly TanggalDibuat);

public interface IReportInput
{
    string? HasilPelaksanaan { get; }
    string? Kendala { get; }
    string? Evaluasi { get; }
    string? Catatan { get; }
}

/// <summary>Isi naratif opsional; boleh dikirim kosong dan dilengkapi lewat PUT.</summary>
public record CreateReportDto(string? HasilPelaksanaan, string? Kendala, string? Evaluasi, string? Catatan) : IReportInput;

/// <summary>Hanya isi naratif. TotalPengeluaran tidak bisa diubah.</summary>
public record UpdateReportDto(string? HasilPelaksanaan, string? Kendala, string? Evaluasi, string? Catatan) : IReportInput;
