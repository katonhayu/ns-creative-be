using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Dashboard;
using NsCreative.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace NsCreative.Core.Services;

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken ct = default);
}

/// <summary>
/// Ringkasan halaman depan. Aturannya sama dengan mode mock frontend (dashboard.api.ts).
/// Role pic hanya melihat event miliknya (lewat <see cref="IAksesEvent.Terlihat"/>).
/// </summary>
public class DashboardService(IAppDbContext db, IAksesEvent akses) : IDashboardService
{
    /// <summary>Task masuk daftar jika deadline-nya dalam 7 hari ke depan atau sudah lewat.</summary>
    public const int HariDeadline = 7;
    public const int JumlahTerdekat = 5;
    public const int JumlahDaftar = 10;

    public async Task<DashboardDto> GetAsync(CancellationToken ct = default)
    {
        var hariIni = Waktu.HariIni();
        var batasDeadline = hariIni.AddDays(HariDeadline);
        var awalTahun = new DateOnly(hariIni.Year, 1, 1);
        var awalTahunDepan = awalTahun.AddYears(1);

        // Nama client/vendor yang sudah dihapus tetap ditampilkan.
        var events = akses.Terlihat(db.Events.AsNoTracking().IgnoreQueryFilters());

        // Berlangsung menurut status, atau hari ini masuk rentang tanggalnya walau status belum diperbarui.
        var berlangsung = await events
            .Where(e => e.Status == EventStatus.Berlangsung
                || (e.Status != EventStatus.Selesai && e.Status != EventStatus.Dibatalkan
                    && e.TanggalMulai <= hariIni && (e.TanggalSelesai ?? e.TanggalMulai) >= hariIni))
            .OrderBy(e => e.TanggalMulai)
            .Take(JumlahDaftar)
            .Select(e => new
            {
                e.Id, e.Nama, e.TanggalMulai, e.TanggalSelesai, e.Lokasi, e.Status,
                NamaPic = e.PicUser.Nama,
                Total = e.Tasks.Count(),
                Selesai = e.Tasks.Count(t => t.Status == TaskItemStatus.Selesai),
            })
            .ToListAsync(ct);

        var terdekat = await events
            .Where(e => e.TanggalMulai > hariIni && e.Status != EventStatus.Selesai && e.Status != EventStatus.Dibatalkan)
            .OrderBy(e => e.TanggalMulai)
            .ThenBy(e => e.Nama)
            .Take(JumlahTerdekat)
            .Select(e => new { e.Id, e.Nama, e.TanggalMulai, e.TanggalSelesai, e.Lokasi, e.Status })
            .ToListAsync(ct);

        var task = await db.Tasks.AsNoTracking()
            .Where(t => events.Any(e => e.Id == t.EventId))
            .Where(t => t.Status != TaskItemStatus.Selesai
                && t.Deadline != null && t.Deadline <= batasDeadline
                && t.Event.Status != EventStatus.Selesai && t.Event.Status != EventStatus.Dibatalkan)
            .OrderBy(t => t.Deadline)
            .ThenBy(t => t.NamaTugas)
            .Take(JumlahDaftar)
            .Select(t => new DashboardTaskDto(
                t.Id, t.EventId, t.NamaTugas, t.Deadline!.Value, t.Status, t.Event.Nama,
                t.PicUser != null ? t.PicUser.Nama : null,
                t.Deadline < hariIni))
            .ToListAsync(ct);

        // Event selesai tetap ikut: pelunasan vendor sering terjadi setelah acara.
        var vendor = await db.EventVendors.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(ev => events.Any(e => e.Id == ev.EventId))
            .Where(ev => ev.StatusPembayaran != StatusPembayaran.Lunas && ev.Event.Status != EventStatus.Dibatalkan)
            .OrderByDescending(ev => ev.Biaya)
            .ThenBy(ev => ev.Event.TanggalMulai)
            .Take(JumlahDaftar)
            .Select(ev => new DashboardVendorDto(
                ev.Id, ev.EventId, ev.Vendor.Nama, ev.Event.Nama, ev.Kebutuhan, ev.Biaya, ev.StatusPembayaran))
            .ToListAsync(ct);

        var tahunIni = events.Where(e =>
            e.TanggalMulai >= awalTahun && e.TanggalMulai < awalTahunDepan && e.Status != EventStatus.Dibatalkan);
        var statistik = new DashboardStatistikDto(
            await tahunIni.CountAsync(ct),
            await tahunIni.CountAsync(e => e.Status == EventStatus.Selesai, ct),
            await tahunIni.Select(e => e.ClientId).Distinct().CountAsync(ct));

        return new DashboardDto(
            berlangsung.Select(e => new DashboardEventBerlangsungDto(
                e.Id, e.Nama, e.TanggalMulai, e.TanggalSelesai, e.Lokasi, e.Status, e.NamaPic,
                e.Total == 0 ? 0 : Math.Round(e.Selesai * 100.0 / e.Total, 1))).ToList(),
            terdekat.Select(e => new DashboardEventTerdekatDto(
                e.Id, e.Nama, e.TanggalMulai, e.TanggalSelesai, e.Lokasi, e.Status,
                e.TanggalMulai.DayNumber - hariIni.DayNumber)).ToList(),
            task,
            vendor,
            statistik);
    }
}
