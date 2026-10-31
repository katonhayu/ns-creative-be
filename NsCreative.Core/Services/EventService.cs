using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Events;
using NsCreative.Core.Entities;
using NsCreative.Core.Enums;
using NsCreative.Core.Exceptions;
using FluentValidation;
using FluentValidation.Results;
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
        // Crew hanya melihat event yang ia pegang atau tempat ia ditugaskan.
        var q = akses.Terlihat(Events);
        if (filter.Status is { } status) q = q.Where(e => e.Status == status);
        if (filter.ClientId is { } clientId) q = q.Where(e => e.ClientId == clientId);
        if (filter.PicCrewId is { } picCrewId) q = q.Where(e => e.PicCrewId == picCrewId);
        // Irisan rentang: event 20–22 Nov ikut tampil untuk filter 21–30 Nov.
        if (filter.DariTanggal is { } dari) q = q.Where(e => (e.TanggalSelesai ?? e.TanggalMulai) >= dari);
        if (filter.SampaiTanggal is { } sampai) q = q.Where(e => e.TanggalMulai <= sampai);

        var (semua, saya) = (pengguna.Role == UserRole.Admin, AkunCrew);
        return await q
            .Cari(query.Search, e => e.Nama, e => e.Jenis, e => e.Lokasi, e => e.Client.Nama, e => e.Client.Perusahaan)
            .Urutkan(query, Urutan)
            .Select(e => new EventListDto(
                e.Id, e.Nama, e.Jenis, e.TanggalMulai, e.TanggalSelesai, e.Lokasi, e.Deskripsi, e.Status,
                e.ClientId, e.Client.Nama, e.Client.Perusahaan, e.PicCrewId, e.PicCrew.Nama,
                semua || (saya != null && e.PicCrew.UserId == saya)))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<EventDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var info = await akses.LihatAsync(id, ct);
        var e = await Events
            .Where(e => e.Id == id)
            .Select(e => new
            {
                e.Id, e.Nama, e.Jenis, e.TanggalMulai, e.TanggalSelesai, e.Lokasi, e.Deskripsi, e.Status,
                e.ClientId, NamaClient = e.Client.Nama, e.Client.Perusahaan, e.Client.Telepon, EmailClient = e.Client.Email,
                e.PicCrewId, NamaPic = e.PicCrew.Nama, TeleponPic = e.PicCrew.Telepon,
                EmailPic = e.PicCrew.User != null ? e.PicCrew.User.Email : null,
                e.CreatedAt, e.UpdatedAt,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Event tidak ditemukan.");

        return new EventDto(
            e.Id, e.Nama, e.Jenis, e.TanggalMulai, e.TanggalSelesai, e.Lokasi, e.Deskripsi, e.Status,
            e.Status.StatusBerikutnya(),
            e.ClientId, e.NamaClient, e.Perusahaan, e.Telepon, e.EmailClient,
            e.PicCrewId, e.NamaPic, e.TeleponPic, e.EmailPic, info.BolehKelola, e.CreatedAt, e.UpdatedAt);
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
        var picCrewId = await PicEventBaruAsync(dto.PicCrewId, ct);
        await CekClientAsync(dto.ClientId, ct);

        var ev = new Event { Status = EventStatus.Draft, PicCrewId = picCrewId };
        Terapkan(ev, dto);
        db.Events.Add(ev);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(ev.Id, ct);
    }

    public async Task<EventDto> UpdateAsync(Guid id, UpdateEventDto dto, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(dto, ct);
        await akses.KelolaAsync(id, ct);
        var ev = await CariAsync(id, ct);

        // Dicek hanya jika berubah, supaya event lama dengan client yang kini dihapus
        // atau PIC yang kini nonaktif tetap bisa diedit. PIC kosong berarti tidak diubah.
        if (dto.ClientId != ev.ClientId) await CekClientAsync(dto.ClientId, ct);
        if (dto.PicCrewId is { } picBaru && picBaru != ev.PicCrewId)
        {
            if (pengguna.Role != UserRole.Admin)
                throw new ForbiddenException("Hanya admin yang bisa memindahkan event ke PIC lain.");
            await CekPicAsync(picBaru, ct);
            ev.PicCrewId = picBaru;
        }

        Terapkan(ev, dto);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<EventDto> UpdateStatusAsync(Guid id, UpdateEventStatusDto dto, CancellationToken ct = default)
    {
        await statusValidator.ValidateAndThrowAsync(dto, ct);
        await akses.KelolaAsync(id, ct);
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

    /// <summary>Akun pengguna login jika ia berperan crew; dipakai untuk mengenali event yang ia pegang.</summary>
    private Guid? AkunCrew => pengguna.Role == UserRole.Crew ? pengguna.Id : null;

    /// <summary>
    /// PIC event baru. Crew yang membuat event otomatis menjadi PIC-nya, jadi akunnya harus sudah
    /// ditautkan ke data crew. Admin wajib memilih PIC.
    /// </summary>
    private async Task<Guid> PicEventBaruAsync(Guid? dipilih, CancellationToken ct)
    {
        if (pengguna.Role == UserRole.Crew)
        {
            var saya = pengguna.Id;
            var crewSaya = await db.Crews.AsNoTracking()
                .Where(c => c.UserId == saya)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(ct)
                ?? throw new BusinessRuleException(
                    "Akun Anda belum ditautkan ke data crew. Minta admin menautkannya di menu Crew sebelum membuat event.");
            if (dipilih is { } lain && lain != crewSaya)
                throw new ForbiddenException("Anda hanya bisa membuat event dengan diri sendiri sebagai PIC.");
            return crewSaya;
        }

        if (dipilih is not { } picCrewId)
            throw new ValidationException([new ValidationFailure(nameof(CreateEventDto.PicCrewId), "PIC harus dipilih.")]);
        await CekPicAsync(picCrewId, ct);
        return picCrewId;
    }

    /// <summary>
    /// PIC event harus crew yang belum dihapus dan punya akun login aktif, karena PIC-lah yang
    /// mengelola event lewat aplikasi.
    /// </summary>
    private async Task CekPicAsync(Guid crewId, CancellationToken ct)
    {
        var pic = await db.Crews.AsNoTracking()
            .Where(c => c.Id == crewId)
            .Select(c => new { c.Nama, PunyaAkun = c.User != null, AkunAktif = c.User != null && c.User.IsAktif })
            .FirstOrDefaultAsync(ct)
            ?? throw new BusinessRuleException("Crew PIC yang dipilih tidak ditemukan atau sudah dihapus.");
        if (!pic.PunyaAkun)
            throw new BusinessRuleException(
                $"{pic.Nama} belum punya akun login, sehingga tidak bisa mengelola event sebagai PIC. " +
                "Tautkan akunnya di menu Crew terlebih dahulu.");
        if (!pic.AkunAktif)
            throw new BusinessRuleException($"Akun {pic.Nama} sedang nonaktif dan tidak bisa menjadi PIC event.");
    }

    private static void Terapkan(Event ev, IEventInput dto)
    {
        ev.ClientId = dto.ClientId;
        ev.Nama = Teks.Wajib(dto.Nama);
        ev.Jenis = Teks.Wajib(dto.Jenis);
        ev.TanggalMulai = dto.TanggalMulai;
        ev.TanggalSelesai = dto.TanggalSelesai;
        ev.Lokasi = Teks.Bersihkan(dto.Lokasi);
        ev.Deskripsi = Teks.Bersihkan(dto.Deskripsi);
    }
}
