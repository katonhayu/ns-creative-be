using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Reports;
using NsCreative.Core.Entities;
using NsCreative.Core.Enums;
using NsCreative.Core.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace NsCreative.Core.Services;

public interface IReportService
{
    Task<PagedResult<ReportListDto>> GetListAsync(QueryParams query, int? tahun, CancellationToken ct = default);
    Task<ReportDto> GetByEventAsync(Guid eventId, CancellationToken ct = default);
    Task<IReadOnlyList<LaporanEventDto>> GetEventSelesaiAsync(int? tahun, CancellationToken ct = default);
    Task<ReportDto> CreateAsync(Guid eventId, CreateReportDto? dto, CancellationToken ct = default);
    Task<ReportDto> UpdateAsync(Guid eventId, UpdateReportDto dto, CancellationToken ct = default);
}

/// <summary>
/// Laporan evaluasi, satu per event, hanya untuk event yang sudah selesai.
/// TotalPengeluaran dibekukan saat dibuat; koreksi budget sesudahnya tidak mengubah laporan lama.
/// </summary>
public class ReportService(
    IAppDbContext db,
    IAksesEvent akses,
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
    /// Event selesai yang terlihat oleh pengguna (pic: miliknya) beserta laporannya, terbaru dulu.
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

        var ev = await akses.LihatAsync(eventId, ct);
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
        await akses.LihatAsync(eventId, ct);
        var laporan = await db.Reports.FirstOrDefaultAsync(r => r.EventId == eventId, ct)
            ?? throw new NotFoundException("Laporan untuk event ini belum dibuat.");

        Terapkan(laporan, dto);
        await db.SaveChangesAsync(ct);

        return await GetByEventAsync(eventId, ct);
    }

    private static void Terapkan(Report laporan, IReportInput dto)
    {
        laporan.HasilPelaksanaan = Teks.Bersihkan(dto.HasilPelaksanaan);
        laporan.Kendala = Teks.Bersihkan(dto.Kendala);
        laporan.Evaluasi = Teks.Bersihkan(dto.Evaluasi);
        laporan.Catatan = Teks.Bersihkan(dto.Catatan);
    }
}
