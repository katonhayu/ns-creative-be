using NsCreative.Core.Common;
using NsCreative.Core.Entities;
using NsCreative.Core.Enums;
using NsCreative.Core.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace NsCreative.Core.Services;

/// <param name="TanggalSelesai">Sama dengan tanggal mulai untuk event satu hari.</param>
public sealed record InfoEvent(Guid Id, string Nama, EventStatus Status, DateOnly TanggalMulai, DateOnly TanggalSelesai);

/// <summary>
/// Pemeriksaan event induk untuk semua endpoint di bawah <c>/api/events/{eventId}/...</c>:
/// ada (404), boleh diakses pengguna login (403), dan belum terkunci (422).
/// Peran per endpoint dicek [Authorize] di controller; di sini kepemilikan datanya.
/// </summary>
public interface IAksesEvent
{
    /// <summary>Untuk baca: event ada dan terlihat oleh pengguna login.</summary>
    Task<InfoEvent> LihatAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>Untuk POST, PUT, PATCH, DELETE: seperti Lihat, dan event belum selesai/dibatalkan.</summary>
    Task<InfoEvent> UbahAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// Event yang terlihat: admin semua; pic hanya yang PIC-nya dirinya; crew hanya event tempat
    /// ia ditugaskan, atau menjadi PIC task/sesi rundown di event itu.
    /// </summary>
    IQueryable<Event> Terlihat(IQueryable<Event> events);

    /// <summary>PIC jadwal atau task: akun yang ada dan aktif, peran apa pun (termasuk crew).</summary>
    Task CekPenanggungJawabAsync(IEnumerable<Guid?> userIds, CancellationToken ct = default);
}

public class AksesEvent(IAppDbContext db, IPenggunaSaatIni pengguna) : IAksesEvent
{
    public async Task<InfoEvent> LihatAsync(Guid eventId, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => new InfoEvent(e.Id, e.Nama, e.Status, e.TanggalMulai, e.TanggalSelesai ?? e.TanggalMulai))
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Event tidak ditemukan.");

        if (pengguna.Role != UserRole.Admin
            && !await Terlihat(db.Events.IgnoreQueryFilters().Where(e => e.Id == eventId)).AnyAsync(ct))
            throw new ForbiddenException(pengguna.Role == UserRole.Pic
                ? "Anda bukan PIC event ini."
                : "Anda tidak ditugaskan di event ini.");
        return ev;
    }

    public async Task<InfoEvent> UbahAsync(Guid eventId, CancellationToken ct = default)
    {
        var ev = await LihatAsync(eventId, ct);
        if (ev.Status.Terkunci())
            throw new BusinessRuleException(
                $"Event {ev.Nama} sudah {ev.Status.Label()}, sehingga datanya tidak bisa diubah lagi.");
        return ev;
    }

    public IQueryable<Event> Terlihat(IQueryable<Event> events)
    {
        var id = pengguna.Id;
        return pengguna.Role switch
        {
            UserRole.Admin => events,
            UserRole.Pic => events.Where(e => e.PicUserId == id),
            _ => events.Where(e =>
                db.EventCrews.Any(ec => ec.EventId == e.Id && ec.Crew.UserId == id)
                || e.Tasks.Any(t => t.PicUserId == id)
                || e.Schedules.Any(s => s.PicUserId == id)),
        };
    }

    public async Task CekPenanggungJawabAsync(IEnumerable<Guid?> userIds, CancellationToken ct = default)
    {
        var ids = userIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0) return;

        var jumlahAktif = await db.Users.CountAsync(u => ids.Contains(u.Id) && u.IsAktif, ct);
        if (jumlahAktif < ids.Count)
            throw new BusinessRuleException("PIC yang dipilih tidak ditemukan atau akunnya sedang nonaktif.");
    }
}
