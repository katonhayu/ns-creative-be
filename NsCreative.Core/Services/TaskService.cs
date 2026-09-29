using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Tasks;
using NsCreative.Core.Entities;
using NsCreative.Core.Enums;
using NsCreative.Core.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace NsCreative.Core.Services;

public interface ITaskService
{
    Task<PagedResult<TaskDto>> GetListAsync(Guid eventId, QueryParams query, TaskFilter filter, CancellationToken ct = default);
    Task<TaskDto> GetByIdAsync(Guid eventId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TaskDto>> GetLintasEventAsync(TaskLintasEventFilter filter, CancellationToken ct = default);
    Task<PagedResult<MyTaskDto>> GetMineAsync(QueryParams query, TaskItemStatus? status, CancellationToken ct = default);
    Task<TaskDto> CreateAsync(Guid eventId, CreateTaskDto dto, CancellationToken ct = default);
    Task<TaskDto> UpdateAsync(Guid eventId, Guid id, UpdateTaskDto dto, CancellationToken ct = default);
    Task<TaskDto> UpdateStatusAsync(Guid eventId, Guid id, UpdateTaskStatusDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid eventId, Guid id, CancellationToken ct = default);
}

/// <summary>
/// Task persiapan event. Status <c>terlambat</c> tidak pernah disimpan; dihitung saat baca
/// dari deadline dan dikirim sebagai <c>isTerlambat</c>. Crew hanya melihat dan mengubah
/// status task yang PIC-nya dirinya.
/// </summary>
public class TaskService(
    IAppDbContext db,
    IAksesEvent akses,
    IPenggunaSaatIni pengguna,
    IValidator<CreateTaskDto> createValidator,
    IValidator<UpdateTaskDto> updateValidator,
    IValidator<UpdateTaskStatusDto> statusValidator) : ITaskService
{
    private static readonly PetaUrutan<TaskItem> Urutan = new PetaUrutan<TaskItem>()
        .Kolom("deadline", t => t.Deadline, bawaan: true, nullDiBawah: true, lanjutan: t => t.NamaTugas)
        .Kolom("namaTugas", t => t.NamaTugas)
        // Mengikuti alur pengerjaan, bukan urutan abjad.
        .Kolom("status", t =>
            t.Status == TaskItemStatus.Belum ? 0 :
            t.Status == TaskItemStatus.Dikerjakan ? 1 : 2, lanjutan: t => t.NamaTugas)
        .Kolom("createdAt", t => t.CreatedAt);

    public const int LintasEventMaks = 500;

    private bool IsCrew => pengguna.Role == UserRole.Crew;

    public async Task<PagedResult<TaskDto>> GetListAsync(
        Guid eventId, QueryParams query, TaskFilter filter, CancellationToken ct = default)
    {
        await akses.LihatAsync(eventId, ct);
        var hariIni = Waktu.HariIni();

        var q = FilterStatus(Tugas(eventId), filter.Status, hariIni);
        if (IsCrew)
        {
            var saya = pengguna.Id;
            q = q.Where(t => t.PicUserId == saya);
        }
        else if (filter.PicUserId is { } picUserId)
        {
            q = q.Where(t => t.PicUserId == picUserId);
        }

        return await Proyeksi(q
                .Cari(query.Search, t => t.NamaTugas, t => t.Deskripsi, t => t.PicUser!.Nama)
                .Urutkan(query, Urutan), hariIni)
            .ToPagedResultAsync(query, ct);
    }

    public async Task<TaskDto> GetByIdAsync(Guid eventId, Guid id, CancellationToken ct = default)
    {
        await akses.LihatAsync(eventId, ct);
        var task = await Proyeksi(Tugas(eventId).Where(t => t.Id == id), Waktu.HariIni()).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Task tidak ditemukan.");
        if (IsCrew && task.PicUserId != pengguna.Id)
            throw new ForbiddenException("Anda hanya bisa melihat task milik Anda sendiri.");
        return task;
    }

    /// <summary>
    /// Task semua event yang terlihat (kecuali event dibatalkan), urut deadline, maksimal
    /// <see cref="LintasEventMaks"/> baris. Tidak berpaginasi karena halaman Task memfilter di klien.
    /// </summary>
    public async Task<IReadOnlyList<TaskDto>> GetLintasEventAsync(TaskLintasEventFilter filter, CancellationToken ct = default)
    {
        var hariIni = Waktu.HariIni();
        var terlihat = akses.Terlihat(db.Events.IgnoreQueryFilters());
        var q = db.Tasks.AsNoTracking()
            .Where(t => terlihat.Any(e => e.Id == t.EventId) && t.Event.Status != EventStatus.Dibatalkan);
        if (filter.EventId is { } eventId) q = q.Where(t => t.EventId == eventId);
        if (filter.MilikSaya || IsCrew)
        {
            var saya = pengguna.Id;
            q = q.Where(t => t.PicUserId == saya);
        }

        return await Proyeksi(FilterStatus(q, filter.Status, hariIni)
                .OrderBy(t => t.Deadline == null)
                .ThenBy(t => t.Deadline)
                .ThenBy(t => t.NamaTugas)
                .Take(LintasEventMaks), hariIni)
            .ToListAsync(ct);
    }

    /// <summary>Task milik pengguna login di semua event (kecuali event dibatalkan), urut deadline.</summary>
    public async Task<PagedResult<MyTaskDto>> GetMineAsync(
        QueryParams query, TaskItemStatus? status, CancellationToken ct = default)
    {
        var hariIni = Waktu.HariIni();
        var saya = pengguna.Id;
        var q = db.Tasks.AsNoTracking()
            .Where(t => t.PicUserId == saya && t.Event.Status != EventStatus.Dibatalkan);

        return await FilterStatus(q, status, hariIni)
            .Cari(query.Search, t => t.NamaTugas, t => t.Event.Nama)
            .Urutkan(query, Urutan)
            .Select(t => new MyTaskDto(
                t.Id, t.EventId, t.Event.Nama, t.Event.Status, t.Event.TanggalMulai,
                t.NamaTugas, t.Deskripsi, t.Deadline, t.Status,
                t.Status != TaskItemStatus.Selesai && t.Deadline != null && t.Deadline < hariIni,
                t.Catatan))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<TaskDto> CreateAsync(Guid eventId, CreateTaskDto dto, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(dto, ct);
        await akses.UbahAsync(eventId, ct);
        await akses.CekPenanggungJawabAsync([dto.PicUserId], ct);

        var task = new TaskItem { EventId = eventId, Status = TaskItemStatus.Belum };
        Terapkan(task, dto);
        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(eventId, task.Id, ct);
    }

    public async Task<TaskDto> UpdateAsync(Guid eventId, Guid id, UpdateTaskDto dto, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(dto, ct);
        await akses.UbahAsync(eventId, ct);
        var task = await CariAsync(eventId, id, ct);
        // Dicek hanya jika berubah, supaya task dengan PIC yang kini nonaktif tetap bisa diedit.
        if (dto.PicUserId != task.PicUserId) await akses.CekPenanggungJawabAsync([dto.PicUserId], ct);

        Terapkan(task, dto);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(eventId, id, ct);
    }

    /// <summary>Crew boleh memanggil ini, tapi hanya untuk task yang PIC-nya dirinya.</summary>
    public async Task<TaskDto> UpdateStatusAsync(Guid eventId, Guid id, UpdateTaskStatusDto dto, CancellationToken ct = default)
    {
        await statusValidator.ValidateAndThrowAsync(dto, ct);
        await akses.UbahAsync(eventId, ct);
        var task = await CariAsync(eventId, id, ct);
        if (IsCrew && task.PicUserId != pengguna.Id)
            throw new ForbiddenException("Anda hanya bisa mengubah status task milik Anda sendiri.");

        task.Status = dto.Status!.Value;
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(eventId, id, ct);
    }

    public async Task DeleteAsync(Guid eventId, Guid id, CancellationToken ct = default)
    {
        await akses.UbahAsync(eventId, ct);
        var task = await CariAsync(eventId, id, ct);

        db.Tasks.Remove(task);
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<TaskItem> Tugas(Guid eventId) =>
        db.Tasks.AsNoTracking().Where(t => t.EventId == eventId);

    private static IQueryable<TaskItem> FilterStatus(IQueryable<TaskItem> q, TaskItemStatus? status, DateOnly hariIni) =>
        status switch
        {
            null => q,
            TaskItemStatus.Terlambat => q.Where(t => t.Status != TaskItemStatus.Selesai && t.Deadline < hariIni),
            var s => q.Where(t => t.Status == s),
        };

    private static IQueryable<TaskDto> Proyeksi(IQueryable<TaskItem> q, DateOnly hariIni) =>
        q.Select(t => new TaskDto(
            t.Id, t.EventId, t.Event.Nama, t.NamaTugas, t.Deskripsi, t.Deadline, t.Status,
            // Tanpa pengecekan null, perbandingan dengan deadline kosong menghasilkan NULL di SQL.
            t.Status != TaskItemStatus.Selesai && t.Deadline != null && t.Deadline < hariIni,
            t.Catatan, t.PicUserId, t.PicUser != null ? t.PicUser.Nama : null,
            t.CreatedAt, t.UpdatedAt));

    private async Task<TaskItem> CariAsync(Guid eventId, Guid id, CancellationToken ct) =>
        await db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.EventId == eventId, ct)
        ?? throw new NotFoundException("Task tidak ditemukan.");

    private static void Terapkan(TaskItem task, ITaskInput dto)
    {
        task.NamaTugas = Teks.Wajib(dto.NamaTugas);
        task.Deskripsi = Teks.Bersihkan(dto.Deskripsi);
        task.Deadline = dto.Deadline;
        task.Catatan = Teks.Bersihkan(dto.Catatan);
        task.PicUserId = dto.PicUserId;
    }
}
