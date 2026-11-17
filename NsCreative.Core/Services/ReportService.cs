using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Reports;
using NsCreative.Core.Entities;
using NsCreative.Core.Enums;
using NsCreative.Core.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace NsCreative.Core.Services;

public interface IReportService
{
    Task<PagedResult<ReportListDto>> GetListAsync(QueryParams query, int? tahun, CancellationToken ct = default);
    Task<ReportDto> GetByEventAsync(Guid eventId, CancellationToken ct = default);
    Task<IReadOnlyList<LaporanEventDto>> GetEventSelesaiAsync(int? tahun, CancellationToken ct = default);
    Task<ReportDto> CreateAsync(Guid eventId, CreateReportDto? dto, CancellationToken ct = default);
    Task<ReportDto> UpdateAsync(Guid eventId, UpdateReportDto dto, CancellationToken ct = default);

    /// <summary>PDF laporan pertanggungjawaban satu event. 404 jika laporannya belum dibuat.</summary>
    Task<FilePdf> PdfLaporanEventAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>PDF rekap event selesai yang terlihat oleh pengguna, sesuai filter halaman Laporan.</summary>
    Task<FilePdf> PdfRekapAsync(RekapLaporanFilter filter, CancellationToken ct = default);
}

/// <summary>
/// Laporan evaluasi, satu per event, hanya untuk event yang sudah selesai.
/// TotalPengeluaran dibekukan saat dibuat; koreksi budget sesudahnya tidak mengubah laporan lama.
/// </summary>
public class ReportService(
    IAppDbContext db,
    IAksesEvent akses,
    IPenggunaSaatIni pengguna,
    IPembuatPdfLaporan pdf,
    IValidator<CreateReportDto> createValidator,
    IValidator<UpdateReportDto> updateValidator) : IReportService
{
    private static readonly PetaUrutan<Report> Urutan = new PetaUrutan<Report>()
        .Kolom("tanggalDibuat", r => r.TanggalDibuat, bawaan: true, lanjutan: r => r.Event.Nama, menurunBawaan: true)
        .Kolom("tanggalMulai", r => r.Event.TanggalMulai, lanjutan: r => r.Event.Nama, menurunBawaan: true)
        .Kolom("namaEvent", r => r.Event.Nama)
        .Kolom("totalPengeluaran", r => r.TotalPengeluaran, lanjutan: r => r.Event.Nama, menurunBawaan: true);

    /// <summary>Filter <paramref name="tahun"/> memakai tahun tanggal mulai event.</summary>
    public async Task<PagedResult<ReportListDto>> GetListAsync(QueryParams query, int? tahun, CancellationToken ct = default)
    {
        var q = db.Reports.AsNoTracking().IgnoreQueryFilters();
        if (tahun is { } t)
        {
            var awal = new DateOnly(t, 1, 1);
            var akhir = awal.AddYears(1);
            q = q.Where(r => r.Event.TanggalMulai >= awal && r.Event.TanggalMulai < akhir);
        }

        return await q
            .Cari(query.Search, r => r.Event.Nama, r => r.Event.Client.Nama)
            .Urutkan(query, Urutan)
            .Select(r => new ReportListDto(
                r.Id, r.EventId, r.Event.Nama, r.Event.Client.Nama, r.Event.TanggalMulai, r.Event.TanggalSelesai,
                r.TotalPengeluaran, r.TanggalDibuat))
            .ToPagedResultAsync(query, ct);
    }

    /// <summary>
    /// Event selesai yang terlihat oleh pengguna (crew: yang ia pegang atau tempat ia ditugaskan)
    /// beserta laporannya, terbaru dulu.
    /// Filter <paramref name="tahun"/> memakai tahun tanggal mulai event.
    /// </summary>
    public async Task<IReadOnlyList<LaporanEventDto>> GetEventSelesaiAsync(int? tahun, CancellationToken ct = default)
    {
        var q = akses.Terlihat(db.Events.AsNoTracking().IgnoreQueryFilters())
            .Where(e => e.Status == EventStatus.Selesai);
        if (tahun is { } t)
        {
            var awal = new DateOnly(t, 1, 1);
            var akhir = awal.AddYears(1);
            q = q.Where(e => e.TanggalMulai >= awal && e.TanggalMulai < akhir);
        }

        return await q
            .OrderByDescending(e => e.TanggalSelesai ?? e.TanggalMulai)
            .ThenBy(e => e.Nama)
            .Select(e => new LaporanEventDto(
                e.Id, e.Nama, e.Client.Nama, e.TanggalMulai, e.TanggalSelesai, e.Status,
                e.Documents.Count(),
                e.Report == null
                    ? null
                    : new RingkasanLaporanDto(
                        e.Report.Id, e.Report.HasilPelaksanaan, e.Report.TotalPengeluaran, e.Report.Kendala,
                        e.Report.Evaluasi, e.Report.Catatan, e.Report.TanggalDibuat)))
            .ToListAsync(ct);
    }

    public async Task<ReportDto> GetByEventAsync(Guid eventId, CancellationToken ct = default)
    {
        await akses.LihatAsync(eventId, ct);
        return await db.Reports.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => r.EventId == eventId)
            .Select(r => new ReportDto(
                r.Id, r.EventId, r.Event.Nama, r.Event.Status, r.Event.TanggalMulai, r.Event.TanggalSelesai,
                r.Event.Client.Nama, r.HasilPelaksanaan, r.TotalPengeluaran, r.Kendala, r.Evaluasi, r.Catatan,
                r.TanggalDibuat,
                r.Event.Budgets.Sum(b => b.EstimasiBiaya),
                r.Event.Budgets.Sum(b => b.Sponsorship),
                r.Event.Budgets.Sum(b => b.BiayaAktual),
                r.CreatedAt, r.UpdatedAt))
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Laporan untuk event ini belum dibuat.");
    }

    /// <summary>⚡ Generate laporan: bekukan total biaya aktual saat ini.</summary>
    public async Task<ReportDto> CreateAsync(Guid eventId, CreateReportDto? dto, CancellationToken ct = default)
    {
        dto ??= new CreateReportDto(null, null, null, null);
        await createValidator.ValidateAndThrowAsync(dto, ct);

        var ev = await akses.KelolaAsync(eventId, ct);
        if (ev.Status != EventStatus.Selesai)
            throw new BusinessRuleException(
                $"Laporan hanya bisa dibuat untuk event yang sudah selesai. Status event saat ini {ev.Status.Label()}.");
        if (await db.Reports.AnyAsync(r => r.EventId == eventId, ct))
            throw new BusinessRuleException("Laporan untuk event ini sudah ada. Ubah isinya lewat PUT.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var total = await db.Budgets.Where(b => b.EventId == eventId).SumAsync(b => b.BiayaAktual, ct);
        var laporan = new Report
        {
            EventId = eventId,
            TotalPengeluaran = total,
            TanggalDibuat = Waktu.HariIni(),
        };
        Terapkan(laporan, dto);
        db.Reports.Add(laporan);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetByEventAsync(eventId, ct);
    }

    /// <summary>Ubah isi naratif. Tetap boleh walau event sudah selesai.</summary>
    public async Task<ReportDto> UpdateAsync(Guid eventId, UpdateReportDto dto, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(dto, ct);
        await akses.KelolaAsync(eventId, ct);
        var laporan = await db.Reports.FirstOrDefaultAsync(r => r.EventId == eventId, ct)
            ?? throw new NotFoundException("Laporan untuk event ini belum dibuat.");

        Terapkan(laporan, dto);
        await db.SaveChangesAsync(ct);

        return await GetByEventAsync(eventId, ct);
    }

    /// <summary>
    /// Isi laporan, ringkasan dan rincian anggaran saat dicetak, penugasan, rundown, ringkasan task,
    /// dan daftar dokumen. Nama client, crew, talent, dan vendor yang sudah dihapus tetap dicetak.
    /// </summary>
    public async Task<FilePdf> PdfLaporanEventAsync(Guid eventId, CancellationToken ct = default)
    {
        await akses.LihatAsync(eventId, ct);
        var hariIni = Waktu.HariIni();

        var ev = await db.Events.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(e => e.Id == eventId)
            .Select(e => new
            {
                Info = new InfoEventLaporan(
                    e.Nama, e.Jenis, e.Status, e.TanggalMulai, e.TanggalSelesai, e.Lokasi, e.Deskripsi,
                    e.Client.Nama, e.Client.Perusahaan, e.Client.Telepon, e.Client.Email,
                    e.PicCrew.Nama, e.PicCrew.Telepon, e.PicCrew.User != null ? e.PicCrew.User.Email : null),
                Narasi = e.Report == null
                    ? null
                    : new NarasiLaporan(
                        e.Report.HasilPelaksanaan, e.Report.Kendala, e.Report.Evaluasi, e.Report.Catatan,
                        e.Report.TotalPengeluaran, e.Report.TanggalDibuat),
                Task = new RingkasanTaskLaporan(
                    e.Tasks.Count(),
                    e.Tasks.Count(t => t.Status == TaskItemStatus.Selesai),
                    e.Tasks.Count(t => t.Status != TaskItemStatus.Selesai && t.Deadline != null && t.Deadline < hariIni)),
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Event tidak ditemukan.");
        if (ev.Narasi is null)
            throw new NotFoundException("Laporan untuk event ini belum dibuat.");

        var pos = await db.Budgets.AsNoTracking()
            .Where(b => b.EventId == eventId)
            .OrderBy(b => b.Kategori)
            .ThenBy(b => b.NamaKebutuhan)
            .Select(b => new PosBudgetLaporan(
                b.NamaKebutuhan, b.Kategori, b.EstimasiBiaya, b.BiayaAktual, b.Sponsorship, b.StatusPembayaran))
            .ToListAsync(ct);
        var perKategori = pos
            .GroupBy(p => p.Kategori)
            .Select(g => new AnggaranKategoriLaporan(
                g.Key, g.Sum(p => p.Estimasi), g.Sum(p => p.Aktual), g.Sum(p => p.Sponsorship)))
            .ToList();

        var talent = await db.EventTalents.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(et => et.EventId == eventId)
            .OrderBy(et => et.JadwalTampil == null)
            .ThenBy(et => et.JadwalTampil)
            .ThenBy(et => et.Talent.Nama)
            .Select(et => new { et.Talent.Nama, et.Talent.Jenis, et.FeeDeal, et.JadwalTampil, et.DurasiMenit, et.Status })
            .ToListAsync(ct);

        var vendor = await db.EventVendors.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(v => v.EventId == eventId)
            .OrderBy(v => v.Vendor.Nama)
            .Select(v => new VendorLaporan(v.Vendor.Nama, v.Vendor.JenisJasa, v.Kebutuhan, v.Biaya, v.StatusPembayaran))
            .ToListAsync(ct);

        var crew = await db.EventCrews.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(ec => ec.EventId == eventId)
            .OrderBy(ec => ec.TanggalBertugas == null)
            .ThenBy(ec => ec.TanggalBertugas)
            .ThenBy(ec => ec.Crew.Nama)
            .Select(ec => new CrewLaporan(ec.Crew.Nama, ec.Posisi, ec.TanggalBertugas))
            .ToListAsync(ct);

        var rundown = await db.Schedules.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.EventId == eventId)
            .OrderBy(s => s.Tanggal)
            .ThenBy(s => s.WaktuMulai)
            .Select(s => new SesiLaporan(
                s.Tanggal, s.WaktuMulai, s.WaktuSelesai, s.Kegiatan, s.Lokasi,
                s.PicCrew != null ? s.PicCrew.Nama : null))
            .ToListAsync(ct);

        var dokumen = await db.Documents.AsNoTracking()
            .Where(d => d.EventId == eventId)
            .OrderBy(d => d.Tanggal)
            .ThenBy(d => d.NamaDokumen)
            .Select(d => new DokumenLaporan(d.NamaDokumen, d.Jenis, d.Tanggal))
            .ToListAsync(ct);

        var data = new DataLaporanEvent(
            ev.Info, ev.Narasi, perKategori, pos,
            talent.Select(t => new TalentLaporan(
                t.Nama, t.Jenis, t.FeeDeal, t.JadwalTampil is { } j ? Waktu.KeWib(j) : null, t.DurasiMenit, t.Status)).ToList(),
            vendor, crew, rundown, ev.Task, dokumen,
            await CetakanAsync(ct));
        return new FilePdf(pdf.LaporanEvent(data), TipeDokumen.NamaUnduhan($"Laporan {ev.Info.Nama}", ".pdf"));
    }

    /// <summary>
    /// Periode dicocokkan dengan tanggal selesai event (tanggal mulai untuk event satu hari).
    /// Urutan terbaru dulu.
    /// </summary>
    public async Task<FilePdf> PdfRekapAsync(RekapLaporanFilter filter, CancellationToken ct = default)
    {
        var (dari, sampai) = (filter.DariTanggal, filter.SampaiTanggal);
        if (dari is { } d && sampai is { } s0 && s0 < d)
            throw new ValidationException([new ValidationFailure(
                nameof(filter.SampaiTanggal), "Tanggal akhir tidak boleh lebih awal dari tanggal awal.")]);

        var q = akses.Terlihat(db.Events.AsNoTracking().IgnoreQueryFilters())
            .Where(e => e.Status == EventStatus.Selesai);
        if (dari is { } awal) q = q.Where(e => (e.TanggalSelesai ?? e.TanggalMulai) >= awal);
        if (sampai is { } akhir) q = q.Where(e => (e.TanggalSelesai ?? e.TanggalMulai) <= akhir);
        if (filter.SudahDibuat is { } sudah) q = q.Where(e => (e.Report != null) == sudah);

        var baris = await q
            .Cari(filter.Search, e => e.Nama, e => e.Client.Nama)
            .OrderByDescending(e => e.TanggalSelesai ?? e.TanggalMulai)
            .ThenBy(e => e.Nama)
            .Select(e => new BarisRekapLaporan(
                e.Nama, e.Client.Nama, e.Client.Perusahaan, e.TanggalMulai, e.TanggalSelesai, e.PicCrew.Nama,
                e.Documents.Count(),
                e.Report != null ? e.Report.TanggalDibuat : null,
                e.Report != null ? e.Report.TotalPengeluaran : null))
            .ToListAsync(ct);

        var keterangan = new List<string>();
        if (filter.SudahDibuat is { } s)
            keterangan.Add(s ? "hanya event yang laporannya sudah dibuat" : "hanya event yang laporannya belum dibuat");
        if (Teks.Bersihkan(filter.Search) is { } cari) keterangan.Add($"kata kunci \"{cari}\"");

        var data = new DataRekapLaporan(dari, sampai, keterangan, baris, await CetakanAsync(ct));
        return new FilePdf(pdf.RekapLaporan(data), TipeDokumen.NamaUnduhan($"Rekap Laporan {NamaPeriode(dari, sampai)}", ".pdf"));
    }

    /// <summary>Bagian nama file rekap, misalnya "2026-01-01 sampai 2026-12-31".</summary>
    private static string NamaPeriode(DateOnly? dari, DateOnly? sampai) => (dari, sampai) switch
    {
        ({ } d, { } s) when d == s => $"{d:yyyy-MM-dd}",
        ({ } d, { } s) => $"{d:yyyy-MM-dd} sampai {s:yyyy-MM-dd}",
        ({ } d, null) => $"sejak {d:yyyy-MM-dd}",
        (null, { } s) => $"sampai {s:yyyy-MM-dd}",
        _ => "semua periode",
    };

    private async Task<CetakanLaporan> CetakanAsync(CancellationToken ct)
    {
        var saya = pengguna.Id;
        var nama = await db.Users.AsNoTracking().Where(u => u.Id == saya).Select(u => u.Nama).FirstOrDefaultAsync(ct);
        return new CetakanLaporan(Waktu.Sekarang(), nama ?? "-");
    }

    private static void Terapkan(Report laporan, IReportInput dto)
    {
        laporan.HasilPelaksanaan = Teks.Bersihkan(dto.HasilPelaksanaan);
        laporan.Kendala = Teks.Bersihkan(dto.Kendala);
        laporan.Evaluasi = Teks.Bersihkan(dto.Evaluasi);
        laporan.Catatan = Teks.Bersihkan(dto.Catatan);
    }
}
