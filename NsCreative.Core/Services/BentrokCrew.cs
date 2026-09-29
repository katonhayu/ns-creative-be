using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Crews;
using NsCreative.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace NsCreative.Core.Services;

/// <summary>
/// Deteksi crew yang ditugaskan di event lain pada tanggal yang beririsan. Hanya peringatan,
/// tidak pernah memblokir penugasan.
/// </summary>
internal static class BentrokCrew
{
    /// <summary>Rentang tanggal yang ingin dicek untuk satu crew (inklusif).</summary>
    internal readonly record struct Rentang(Guid CrewId, DateOnly Dari, DateOnly Sampai);

    /// <summary>
    /// Tanggal sebuah penugasan = tanggal bertugas jika diisi, selain itu seluruh rentang event.
    /// Event yang dibatalkan dan <paramref name="kecualiEventId"/> tidak dihitung.
    /// Satu query untuk semua rentang, lalu dicocokkan di memori per crew.
    /// </summary>
    public static async Task<ILookup<Guid, CrewBentrokDto>> CariAsync(
        IAppDbContext db, IReadOnlyCollection<Rentang> daftar, Guid? kecualiEventId, CancellationToken ct)
    {
        if (daftar.Count == 0) return Array.Empty<CrewBentrokDto>().ToLookup(_ => Guid.Empty);

        var crewIds = daftar.Select(d => d.CrewId).Distinct().ToList();
        var dari = daftar.Min(d => d.Dari);
        var sampai = daftar.Max(d => d.Sampai);

        var kandidat = await db.EventCrews.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(ec => crewIds.Contains(ec.CrewId)
                && ec.EventId != kecualiEventId
                && ec.Event.Status != EventStatus.Dibatalkan
                && (ec.TanggalBertugas ?? ec.Event.TanggalMulai) <= sampai
                && (ec.TanggalBertugas ?? ec.Event.TanggalSelesai ?? ec.Event.TanggalMulai) >= dari)
            .OrderBy(ec => ec.Event.TanggalMulai)
            .Select(ec => new
            {
                ec.CrewId,
                Dari = ec.TanggalBertugas ?? ec.Event.TanggalMulai,
                Sampai = ec.TanggalBertugas ?? ec.Event.TanggalSelesai ?? ec.Event.TanggalMulai,
                Bentrok = new CrewBentrokDto(
                    ec.EventId, ec.Event.Nama, ec.Event.TanggalMulai, ec.Event.TanggalSelesai,
                    ec.Event.Status, ec.Posisi, ec.TanggalBertugas),
            })
            .ToListAsync(ct);

        return daftar
            .SelectMany(d => kandidat
                .Where(k => k.CrewId == d.CrewId && k.Dari <= d.Sampai && k.Sampai >= d.Dari)
                .Select(k => (d.CrewId, k.Bentrok)))
            .ToLookup(x => x.CrewId, x => x.Bentrok);
    }
}
