using NsCreative.Core.Enums;

namespace NsCreative.Core.Dtos.Reports;

// Isi dokumen PDF. Bukan respons JSON: dirakit ReportService lalu dicetak IPembuatPdfLaporan.

/// <summary>Laporan pertanggungjawaban satu event yang sudah punya laporan.</summary>
public record DataLaporanEvent(
    InfoEventLaporan Event,
    NarasiLaporan Narasi,
    IReadOnlyList<AnggaranKategoriLaporan> AnggaranPerKategori,
    IReadOnlyList<PosBudgetLaporan> PosBudget,
    IReadOnlyList<TalentLaporan> Talent,
    IReadOnlyList<VendorLaporan> Vendor,
    IReadOnlyList<CrewLaporan> Crew,
    IReadOnlyList<SesiLaporan> Rundown,
    RingkasanTaskLaporan Task,
    IReadOnlyList<DokumenLaporan> Dokumen,
    CetakanLaporan Cetakan);

public record InfoEventLaporan(
    string Nama,
    string Jenis,
    EventStatus Status,
    DateOnly TanggalMulai,
    DateOnly? TanggalSelesai,
    string? Lokasi,
    string? Deskripsi,
    string NamaClient,
    string? PerusahaanClient,
    string? TeleponClient,
    string? EmailClient,
    string NamaPic,
    string? TeleponPic,
    string? EmailPic);

/// <param name="TotalPengeluaran">Dibekukan saat laporan dibuat; bisa berbeda dari biaya aktual saat ini.</param>
public record NarasiLaporan(
    string? HasilPelaksanaan,
    string? Kendala,
    string? Evaluasi,
    string? Catatan,
    decimal TotalPengeluaran,
    DateOnly TanggalDibuat);

public record AnggaranKategoriLaporan(string Kategori, decimal Estimasi, decimal Aktual, decimal Sponsorship);

public record PosBudgetLaporan(
    string NamaKebutuhan,
    string Kategori,
    decimal Estimasi,
    decimal Aktual,
    decimal Sponsorship,
    StatusPembayaran StatusPembayaran);

/// <param name="JadwalTampil">Dalam WIB.</param>
public record TalentLaporan(
    string Nama,
    string Jenis,
    decimal FeeDeal,
    DateTime? JadwalTampil,
    int? DurasiMenit,
    TalentStatus Status);

public record VendorLaporan(
    string Nama,
    string JenisJasa,
    string? Kebutuhan,
    decimal Biaya,
    StatusPembayaran StatusPembayaran);

public record CrewLaporan(string Nama, string Posisi, DateOnly? TanggalBertugas);

public record SesiLaporan(
    DateOnly Tanggal,
    TimeOnly WaktuMulai,
    TimeOnly? WaktuSelesai,
    string Kegiatan,
    string? Lokasi,
    string? NamaPic);

/// <param name="Terlambat">Belum selesai dan deadline-nya sudah lewat pada saat dicetak.</param>
public record RingkasanTaskLaporan(int Total, int Selesai, int Terlambat);

public record DokumenLaporan(string Nama, string Jenis, DateOnly Tanggal);

/// <param name="Pada">Waktu cetak dalam WIB.</param>
/// <param name="Oleh">Nama pengguna yang mengunduh.</param>
public record CetakanLaporan(DateTime Pada, string Oleh);

/// <summary>Rekap event selesai pada satu periode.</summary>
/// <param name="Dari">Awal periode (inklusif). Null = tanpa batas awal.</param>
/// <param name="Sampai">Akhir periode (inklusif). Null = tanpa batas akhir.</param>
/// <param name="Filter">Keterangan filter lain yang aktif, untuk dicetak di bawah judul.</param>
public record DataRekapLaporan(
    DateOnly? Dari,
    DateOnly? Sampai,
    IReadOnlyList<string> Filter,
    IReadOnlyList<BarisRekapLaporan> Baris,
    CetakanLaporan Cetakan);

/// <param name="TanggalLaporan">Null jika laporan belum dibuat.</param>
/// <param name="TotalPengeluaran">Total yang dibekukan di laporan. Null jika laporan belum dibuat.</param>
public record BarisRekapLaporan(
    string NamaEvent,
    string NamaClient,
    string? PerusahaanClient,
    DateOnly TanggalMulai,
    DateOnly? TanggalSelesai,
    string NamaPic,
    int JumlahDokumen,
    DateOnly? TanggalLaporan,
    decimal? TotalPengeluaran);

/// <summary>
/// Filter rekap PDF. Semua opsional. Periode dicocokkan dengan tanggal selesai event
/// (tanggal mulai untuk event satu hari), yaitu kapan event itu rampung.
/// </summary>
public class RekapLaporanFilter
{
    /// <summary>Awal periode (inklusif).</summary>
    public DateOnly? DariTanggal { get; set; }

    /// <summary>Akhir periode (inklusif). Tidak boleh lebih awal dari <see cref="DariTanggal"/>.</summary>
    public DateOnly? SampaiTanggal { get; set; }

    /// <summary>true = hanya yang laporannya sudah dibuat, false = yang belum.</summary>
    public bool? SudahDibuat { get; set; }

    /// <summary>Dicari pada nama event dan nama client.</summary>
    public string? Search { get; set; }
}
