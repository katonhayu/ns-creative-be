using NsCreative.Core.Common;
using NsCreative.Core.Entities;
using NsCreative.Core.Enums;
using NsCreative.Core.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace NsCreative.Core.Services;

/// <param name="TanggalSelesai">Sama dengan tanggal mulai untuk event satu hari.</param>
/// <param name="BolehKelola">Pengguna login boleh mengubah data event ini: admin, atau crew yang menjadi PIC-nya.</param>
public sealed record InfoEvent(
    Guid Id, string Nama, EventStatus Status, DateOnly TanggalMulai, DateOnly TanggalSelesai,
    Guid PicCrewId, bool BolehKelola)
{
    /// <summary>Event selesai atau dibatalkan: penugasan, jadwal, task, dan budget tidak boleh diubah lagi.</summary>
    public void CekBelumTerkunci()
    {
        if (Status.Terkunci())
            throw new BusinessRuleException(
                $"Event {Nama} sudah {Status.Label()}, sehingga datanya tidak bisa diubah lagi.");
    }
}

/// <summary>
/// Pemeriksaan event induk untuk semua endpoint di bawah <c>/api/events/{eventId}/...</c>.
/// <list type="bullet">
/// <item>admin: lihat dan kelola semua event.</item>
/// <item>event_manager: lihat semua event, tidak pernah mengubah.</item>
/// <item>crew: kelola event yang PIC-nya crew miliknya; event lain tempat ia ditugaskan hanya dilihat.</item>
/// </list>
/// Peran per endpoint dicek [Authorize] di controller; di sini hubungan pengguna dengan event-nya.
/// </summary>
public interface IAksesEvent
{
    /// <summary>Untuk baca: event ada (404) dan terlihat oleh pengguna login (403).</summary>
    Task<InfoEvent> LihatAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// Untuk perubahan yang tetap boleh walau event sudah selesai (data event, dokumen, laporan):
    /// seperti Lihat, dan pengguna boleh mengelola event ini (403).
    /// </summary>
    Task<InfoEvent> KelolaAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>Untuk POST, PUT, PATCH, DELETE penugasan, jadwal, task, dan budget: Kelola, dan event belum terkunci (422).</summary>
    Task<InfoEvent> UbahAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>Event yang terlihat: admin dan event manager semua; crew event yang ia pegang atau tempat ia ditugaskan.</summary>
    IQueryable<Event> Terlihat(IQueryable<Event> events);

    /// <summary>
    /// PIC task atau sesi rundown harus crew yang terlibat di event: PIC event atau crew yang
    /// ditugaskan di event itu.
    /// </summary>
    Task CekPenanggungJawabAsync(InfoEvent ev, IEnumerable<Guid?> crewIds, CancellationToken ct = default);
}

public class AksesEvent(IAppDbContext db, IPenggunaSaatIni pengguna) : IAksesEvent
{
    public async Task<InfoEvent> LihatAsync(Guid eventId, CancellationToken ct = default)
    {
        var saya = pengguna.Id;
        var role = pengguna.Role;
        var ev = await db.Events.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(e => e.Id == eventId)
            .Select(e => new
            {
                e.Id, e.Nama, e.Status, e.TanggalMulai, TanggalSelesai = e.TanggalSelesai ?? e.TanggalMulai,
                e.PicCrewId,
                SayaPic = e.PicCrew.UserId == saya,
                SayaDitugaskan = e.Crews.Any(ec => ec.Crew.UserId == saya),
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Event tidak ditemukan.");

        var bolehKelola = role == UserRole.Admin || (role == UserRole.Crew && ev.SayaPic);
        var bolehLihat = role is UserRole.Admin or UserRole.EventManager || ev.SayaPic || ev.SayaDitugaskan;
        if (!bolehLihat)
            throw new ForbiddenException("Anda tidak terlibat di event ini.");

        return new InfoEvent(ev.Id, ev.Nama, ev.Status, ev.TanggalMulai, ev.TanggalSelesai, ev.PicCrewId, bolehKelola);
    }

    public async Task<InfoEvent> KelolaAsync(Guid eventId, CancellationToken ct = default)
    {
        var ev = await LihatAsync(eventId, ct);
        if (!ev.BolehKelola)
            throw new ForbiddenException(pengguna.Role == UserRole.EventManager
                ? "Event manager hanya bisa melihat data, tidak bisa mengubahnya."
                : "Hanya PIC event ini yang bisa mengubah datanya.");
        return ev;
    }

    public async Task<InfoEvent> UbahAsync(Guid eventId, CancellationToken ct = default)
    {
        var ev = await KelolaAsync(eventId, ct);
        ev.CekBelumTerkunci();
        return ev;
    }

    public IQueryable<Event> Terlihat(IQueryable<Event> events)
    {
        var saya = pengguna.Id;
        return pengguna.Role switch
        {
            UserRole.Admin or UserRole.EventManager => events,
            _ => events.Where(e => e.PicCrew.UserId == saya || e.Crews.Any(ec => ec.Crew.UserId == saya)),
        };
    }

    public async Task CekPenanggungJawabAsync(InfoEvent ev, IEnumerable<Guid?> crewIds, CancellationToken ct = default)
    {
        var ids = crewIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0) return;

        var ditugaskan = await db.EventCrews.AsNoTracking()
            .Where(ec => ec.EventId == ev.Id && ids.Contains(ec.CrewId))
            .Select(ec => ec.CrewId)
            .ToListAsync(ct);
        var luar = ids.Where(id => id != ev.PicCrewId && !ditugaskan.Contains(id)).ToList();
        if (luar.Count == 0) return;

        var nama = await db.Crews.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => luar.Contains(c.Id))
            .OrderBy(c => c.Nama)
            .Select(c => c.Nama)
            .ToListAsync(ct);
        if (nama.Count < luar.Count)
            throw new BusinessRuleException("PIC yang dipilih tidak ditemukan.");
        throw new BusinessRuleException(
            $"{string.Join(", ", nama)} tidak terlibat di event ini. " +
            "PIC hanya bisa dipilih dari PIC event atau crew yang ditugaskan di tab Crew.");
    }
}
