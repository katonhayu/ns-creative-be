using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Events;
using NsCreative.Core.Entities;
using NsCreative.Core.Enums;
using NsCreative.Core.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace NsCreative.Core.Services;

public interface IEventService
{
    Task<PagedResult<EventListDto>> GetListAsync(QueryParams query, EventFilter filter, CancellationToken ct = default);
    Task<EventDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<EventLookupDto>> GetLookupAsync(string? search, EventStatus? status, CancellationToken ct = default);
    Task<EventSummaryDto> GetSummaryAsync(Guid id, CancellationToken ct = default);
    Task<EventDto> CreateAsync(CreateEventDto dto, CancellationToken ct = default);
    Task<EventDto> UpdateAsync(Guid id, UpdateEventDto dto, CancellationToken ct = default);
    Task<EventDto> UpdateStatusAsync(Guid id, UpdateEventStatusDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class EventService(
    IAppDbContext db,
    IAksesEvent akses,
    IPenggunaSaatIni pengguna,
    IPenyimpananFile penyimpanan,
    IValidator<CreateEventDto> createValidator,
    IValidator<UpdateEventDto> updateValidator,
    IValidator<UpdateEventStatusDto> statusValidator) : IEventService
{
    private static readonly PetaUrutan<Event> Urutan = new PetaUrutan<Event>()
        .Kolom("tanggalMulai", e => e.TanggalMulai, bawaan: true, lanjutan: e => e.Nama)
        .Kolom("nama", e => e.Nama)
        // Mengikuti alur status, bukan urutan abjad.
        .Kolom("status", e =>
            e.Status == EventStatus.Draft ? 0 :
            e.Status == EventStatus.Perencanaan ? 1 :
            e.Status == EventStatus.Dikonfirmasi ? 2 :
            e.Status == EventStatus.Berlangsung ? 3 :
            e.Status == EventStatus.Selesai ? 4 : 5, lanjutan: e => e.Nama)
        .Kolom("createdAt", e => e.CreatedAt);

    /// <summary>
    /// Filter soft delete diabaikan: event lama tetap tampil lengkap dengan nama client-nya
    /// walaupun client tersebut sudah dihapus.
    /// </summary>
    private IQueryable<Event> Events => db.Events.AsNoTracking().IgnoreQueryFilters();

    public async Task<PagedResult<EventListDto>> GetListAsync(
        QueryParams query, EventFilter filter, CancellationToken ct = default)
    {
        // pic hanya melihat event miliknya, crew hanya event tempat ia bertugas.
        var q = akses.Terlihat(Events);
        if (filter.Status is { } status) q = q.Where(e => e.Status == status);
        if (filter.ClientId is { } clientId) q = q.Where(e => e.ClientId == clientId);
        if (filter.PicUserId is { } picUserId) q = q.Where(e => e.PicUserId == picUserId);
        // Irisan rentang: event 20–22 Nov ikut tampil untuk filter 21–30 Nov.
        if (filter.DariTanggal is { } dari) q = q.Where(e => (e.TanggalSelesai ?? e.TanggalMulai) >= dari);
        if (filter.SampaiTanggal is { } sampai) q = q.Where(e => e.TanggalMulai <= sampai);

        return await q
            .Cari(query.Search, e => e.Nama, e => e.Jenis, e => e.Lokasi, e => e.Client.Nama, e => e.Client.Perusahaan)
            .Urutkan(query, Urutan)
            .Select(e => new EventListDto(
                e.Id, e.Nama, e.Jenis, e.TanggalMulai, e.TanggalSelesai, e.Lokasi, e.Deskripsi, e.Status,
                e.ClientId, e.Client.Nama, e.Client.Perusahaan, e.PicUserId, e.PicUser.Nama))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<EventDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await akses.LihatAsync(id, ct);
        var e = await Events
            .Where(e => e.Id == id)
            .Select(e => new
            {
                e.Id, e.Nama, e.Jenis, e.TanggalMulai, e.TanggalSelesai, e.Lokasi, e.Deskripsi, e.Status,
                e.ClientId, NamaClient = e.Client.Nama, e.Client.Perusahaan, e.Client.Telepon, EmailClient = e.Client.Email,
                e.PicUserId, NamaPic = e.PicUser.Nama, EmailPic = e.PicUser.Email,
                e.CreatedAt, e.UpdatedAt,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Event tidak ditemukan.");

        return new EventDto(
            e.Id, e.Nama, e.Jenis, e.TanggalMulai, e.TanggalSelesai, e.Lokasi, e.Deskripsi, e.Status,
            e.Status.StatusBerikutnya(),
            e.ClientId, e.NamaClient, e.Perusahaan, e.Telepon, e.EmailClient,
            e.PicUserId, e.NamaPic, e.EmailPic, e.CreatedAt, e.UpdatedAt);
    }

    public async Task<IReadOnlyList<EventLookupDto>> GetLookupAsync(
        string? search, EventStatus? status, CancellationToken ct = default)
    {
        var q = akses.Terlihat(Events).Cari(search, e => e.Nama, e => e.Client.Nama);
        if (status is not null) q = q.Where(e => e.Status == status);

        return await q
            .OrderByDescending(e => e.TanggalMulai)
            .ThenBy(e => e.Nama)
            .Take(QueryParams.LookupMaks)
            .Select(e => new EventLookupDto(e.Id, e.Nama, e.TanggalMulai, e.TanggalSelesai, e.Status, e.Client.Nama))
            .ToListAsync(ct);
    }

    public async Task<EventSummaryDto> GetSummaryAsync(Guid id, CancellationToken ct = default)
    {
        await akses.LihatAsync(id, ct);
        var hariIni = Waktu.HariIni();
        var s = await Events
            .Where(e => e.Id == id)
            .Select(e => new
            {
                e.Id, e.Nama, e.Status, e.TanggalMulai,
                TaskTotal = e.Tasks.Count(),
                TaskSelesai = e.Tasks.Count(t => t.Status == TaskItemStatus.Selesai),
                TaskTerlambat = e.Tasks.Count(t => t.Status != TaskItemStatus.Selesai && t.Deadline < hariIni),
                Estimasi = e.Budgets.Sum(b => b.EstimasiBiaya),
                Aktual = e.Budgets.Sum(b => b.BiayaAktual),
                Sponsorship = e.Budgets.Sum(b => b.Sponsorship),
                BelumLunas = e.Budgets.Count(b => b.StatusPembayaran != StatusPembayaran.Lunas),
                JumlahTalent = e.Talents.Count(),
                JumlahCrew = e.Crews.Count(),
                JumlahVendor = e.Vendors.Count(),
                JumlahDokumen = e.Documents.Count(),
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Event tidak ditemukan.");

        var persen = s.TaskTotal == 0 ? 0 : Math.Round(s.TaskSelesai * 100.0 / s.TaskTotal, 1);
        return new EventSummaryDto(
            s.Id, s.Nama, s.Status,
            s.TanggalMulai.DayNumber - hariIni.DayNumber,
            new RingkasanTaskDto(s.TaskTotal, s.TaskSelesai, s.TaskTerlambat, persen),
            new RingkasanBudgetDto(s.Estimasi, s.Aktual, s.Estimasi - s.Aktual, s.Sponsorship, s.BelumLunas),
            s.JumlahTalent, s.JumlahCrew, s.JumlahVendor, s.JumlahDokumen);
    }

    public async Task<EventDto> CreateAsync(CreateEventDto dto, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(dto, ct);
        if (pengguna.Role == UserRole.Pic && dto.PicUserId != pengguna.Id)
            throw new ForbiddenException("Sebagai PIC, Anda hanya bisa membuat event dengan diri sendiri sebagai PIC.");
        await CekClientAsync(dto.ClientId, ct);
        await CekPicAsync(dto.PicUserId, ct);

        var ev = new Event { Status = EventStatus.Draft };
        Terapkan(ev, dto);
        db.Events.Add(ev);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(ev.Id, ct);
    }

    public async Task<EventDto> UpdateAsync(Guid id, UpdateEventDto dto, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(dto, ct);
        await akses.LihatAsync(id, ct);
        var ev = await CariAsync(id, ct);
        if (pengguna.Role == UserRole.Pic && dto.PicUserId != pengguna.Id)
            throw new ForbiddenException("Hanya admin yang bisa memindahkan event ke PIC lain.");

        // Dicek hanya jika berubah, supaya event lama dengan client yang kini dihapus
        // atau PIC yang kini nonaktif tetap bisa diedit.
        if (dto.ClientId != ev.ClientId) await CekClientAsync(dto.ClientId, ct);
        if (dto.PicUserId != ev.PicUserId) await CekPicAsync(dto.PicUserId, ct);

        Terapkan(ev, dto);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<EventDto> UpdateStatusAsync(Guid id, UpdateEventStatusDto dto, CancellationToken ct = default)
    {
        await statusValidator.ValidateAndThrowAsync(dto, ct);
        await akses.LihatAsync(id, ct);
        var ev = await CariAsync(id, ct);
        var baru = dto.Status!.Value;

        if (ev.Status == baru)
            throw new BusinessRuleException($"Status event sudah {baru.Label()}.");
        if (!ev.Status.BolehMenjadi(baru))
        {
            var pilihan = ev.Status.StatusBerikutnya();
            throw new BusinessRuleException(pilihan.Count == 0
                ? $"Event yang sudah {ev.Status.Label()} tidak bisa diubah statusnya lagi."
                : $"Status event tidak bisa diubah dari {ev.Status.Label()} ke {baru.Label()}. " +
                  $"Status berikutnya yang diizinkan: {string.Join(" atau ", pilihan.Select(s => s.Label()))}.");
        }

        ev.Status = baru;
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    /// <summary>
    /// Hapus permanen. Penugasan, jadwal, task, budget, dokumen, dan laporan ikut terhapus
    /// oleh database (ON DELETE CASCADE); file dokumennya dihapus setelah itu.
    /// </summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var ev = await CariAsync(id, ct);
        if (!ev.Status.BolehDihapus())
            throw new BusinessRuleException(
                $"Event berstatus {ev.Status.Label()} tidak bisa dihapus. " +
                "Hanya event draft atau dibatalkan yang bisa dihapus.");

        db.Events.Remove(ev);
        await db.SaveChangesAsync(ct);
        penyimpanan.HapusFolderEvent(id);
    }

    private async Task<Event> CariAsync(Guid id, CancellationToken ct) =>
        await db.Events.FirstOrDefaultAsync(e => e.Id == id, ct)
        ?? throw new NotFoundException("Event tidak ditemukan.");

    private async Task CekClientAsync(Guid clientId, CancellationToken ct)
    {
        if (!await db.Clients.AnyAsync(c => c.Id == clientId, ct))
            throw new BusinessRuleException("Client yang dipilih tidak ditemukan atau sudah dihapus.");
    }

    /// <summary>PIC harus akun aktif berperan admin atau pic. Crew tidak boleh melihat budget, jadi tidak bisa jadi PIC.</summary>
    private async Task CekPicAsync(Guid userId, CancellationToken ct)
    {
        var pic = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Nama, u.Role, u.IsAktif })
            .FirstOrDefaultAsync(ct)
            ?? throw new BusinessRuleException("PIC yang dipilih tidak ditemukan.");
        if (pic.Role == UserRole.Crew)
            throw new BusinessRuleException($"{pic.Nama} berperan crew dan tidak bisa menjadi PIC event.");
        if (!pic.IsAktif)
            throw new BusinessRuleException($"Akun {pic.Nama} sedang nonaktif dan tidak bisa menjadi PIC event.");
    }

    private static void Terapkan(Event ev, IEventInput dto)
    {
        ev.ClientId = dto.ClientId;
        ev.PicUserId = dto.PicUserId;
        ev.Nama = Teks.Wajib(dto.Nama);
        ev.Jenis = Teks.Wajib(dto.Jenis);
        ev.TanggalMulai = dto.TanggalMulai;
        ev.TanggalSelesai = dto.TanggalSelesai;
        ev.Lokasi = Teks.Bersihkan(dto.Lokasi);
        ev.Deskripsi = Teks.Bersihkan(dto.Deskripsi);
    }
}
