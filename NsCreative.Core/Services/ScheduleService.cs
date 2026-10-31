using System.Linq.Expressions;
using System.Text.Json;
using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Schedules;
using NsCreative.Core.Entities;
using NsCreative.Core.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace NsCreative.Core.Services;

public interface IScheduleService
{
    Task<IReadOnlyList<RundownHariDto>> GetListAsync(Guid eventId, DateOnly? tanggal, CancellationToken ct = default);
    Task<ScheduleDto> GetByIdAsync(Guid eventId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ScheduleDto>> GetLintasEventAsync(JadwalFilter filter, CancellationToken ct = default);
    Task<ScheduleDto> CreateAsync(Guid eventId, CreateScheduleDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<ScheduleDto>> CreateBulkAsync(Guid eventId, IReadOnlyList<CreateScheduleDto> daftar, CancellationToken ct = default);
    Task<ScheduleDto> UpdateAsync(Guid eventId, Guid id, UpdateScheduleDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid eventId, Guid id, CancellationToken ct = default);
}

/// <summary>Rundown event. Daftar tidak berpaginasi: dikelompokkan per tanggal dan diurutkan jam.</summary>
public class ScheduleService(
    IAppDbContext db,
    IAksesEvent akses,
    IValidator<CreateScheduleDto> createValidator,
    IValidator<UpdateScheduleDto> updateValidator) : IScheduleService
{
    public const int BulkMaks = 200;

    /// <summary>Batas jumlah sesi di daftar jadwal lintas event.</summary>
    public const int LintasEventMaks = 500;

    private static readonly Expression<Func<Schedule, ScheduleDto>> KeDto = s => new ScheduleDto(
        s.Id, s.EventId, s.Event.Nama, s.Tanggal, s.WaktuMulai, s.WaktuSelesai, s.Kegiatan, s.Lokasi,
        s.PicCrewId, s.PicCrew != null ? s.PicCrew.Nama : null,
        s.Tanggal < s.Event.TanggalMulai || s.Tanggal > (s.Event.TanggalSelesai ?? s.Event.TanggalMulai),
        s.CreatedAt, s.UpdatedAt);

    public async Task<IReadOnlyList<RundownHariDto>> GetListAsync(
        Guid eventId, DateOnly? tanggal, CancellationToken ct = default)
    {
        await akses.LihatAsync(eventId, ct);

        var q = Sesi(eventId);
        if (tanggal is { } t) q = q.Where(s => s.Tanggal == t);
        var daftar = await q
            .OrderBy(s => s.Tanggal)
            .ThenBy(s => s.WaktuMulai)
            .ThenBy(s => s.WaktuSelesai)
            .ThenBy(s => s.Kegiatan)
            .Select(KeDto)
            .ToListAsync(ct);

        return daftar
            .GroupBy(s => s.Tanggal)
            .Select(g => new RundownHariDto(g.Key, g.First().DiLuarRentangEvent, g.ToList()))
            .ToList();
    }

    /// <summary>
    /// Jadwal semua event yang terlihat oleh pengguna (crew: event yang ia pegang atau tempat
    /// ia ditugaskan), urut tanggal dan jam. Rentang tanggal opsional. Hasil dibatasi
    /// <see cref="LintasEventMaks"/> sesi dengan tanggal terbaru, supaya pilihan "Semua"
    /// tetap ringan walau riwayat bertambah.
    /// </summary>
    public async Task<IReadOnlyList<ScheduleDto>> GetLintasEventAsync(JadwalFilter filter, CancellationToken ct = default)
    {
        if (filter is { DariTanggal: { } d, SampaiTanggal: { } s } && s < d)
            throw new ValidationException(
                [new ValidationFailure("SampaiTanggal", "Tanggal akhir tidak boleh lebih awal dari tanggal awal.")]);

        var terlihat = akses.Terlihat(db.Events.IgnoreQueryFilters());
        var q = db.Schedules.AsNoTracking().Where(s => terlihat.Any(e => e.Id == s.EventId));
        if (filter.EventId is { } eventId) q = q.Where(s => s.EventId == eventId);
        if (filter.DariTanggal is { } dari) q = q.Where(s => s.Tanggal >= dari);
        if (filter.SampaiTanggal is { } sampai) q = q.Where(s => s.Tanggal <= sampai);

        // Ambil yang terbaru dulu untuk batas jumlah, lalu kembalikan urut kronologis.
        var daftar = await q
            .OrderByDescending(s => s.Tanggal)
            .ThenByDescending(s => s.WaktuMulai)
            .Take(LintasEventMaks)
            .Select(KeDto)
            .ToListAsync(ct);
        return daftar
            .OrderBy(s => s.Tanggal)
            .ThenBy(s => s.WaktuMulai)
            .ThenBy(s => s.NamaEvent)
            .ToList();
    }

    public async Task<ScheduleDto> GetByIdAsync(Guid eventId, Guid id, CancellationToken ct = default)
    {
        await akses.LihatAsync(eventId, ct);
        return await Sesi(eventId).Where(s => s.Id == id).Select(KeDto).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Sesi rundown tidak ditemukan.");
    }

    public async Task<ScheduleDto> CreateAsync(Guid eventId, CreateScheduleDto dto, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(dto, ct);
        var ev = await akses.UbahAsync(eventId, ct);
        await akses.CekPenanggungJawabAsync(ev, [dto.PicCrewId], ct);

        var sesi = new Schedule { EventId = eventId };
        Terapkan(sesi, dto);
        db.Schedules.Add(sesi);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(eventId, sesi.Id, ct);
    }

    /// <summary>
    /// ⚡ Input rundown sekaligus dalam satu transaksi. Semua baris divalidasi dulu;
    /// jika satu saja gagal, tidak ada yang tersimpan. Error diberi awalan indeks, misalnya <c>[2].kegiatan</c>.
    /// </summary>
    public async Task<IReadOnlyList<ScheduleDto>> CreateBulkAsync(
        Guid eventId, IReadOnlyList<CreateScheduleDto> daftar, CancellationToken ct = default)
    {
        if (daftar.Count == 0)
            throw new ValidationException([new ValidationFailure("", "Rundown yang dikirim masih kosong.")]);
        if (daftar.Count > BulkMaks)
            throw new ValidationException([new ValidationFailure("", $"Maksimal {BulkMaks} sesi sekali kirim.")]);

        var gagal = new List<ValidationFailure>();
        for (var i = 0; i < daftar.Count; i++)
        {
            if (daftar[i] is null)
            {
                gagal.Add(new ValidationFailure($"[{i}]", $"Baris {i + 1}: data sesi kosong."));
                continue;
            }
            var hasil = await createValidator.ValidateAsync(daftar[i], ct);
            gagal.AddRange(hasil.Errors.Select(e => new ValidationFailure(
                $"[{i}].{JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName)}", $"Baris {i + 1}: {e.ErrorMessage}")));
        }
        if (gagal.Count > 0) throw new ValidationException(gagal);

        var ev = await akses.UbahAsync(eventId, ct);
        await akses.CekPenanggungJawabAsync(ev, daftar.Select(d => d.PicCrewId), ct);

        var semua = daftar.Select(dto =>
        {
            var sesi = new Schedule { EventId = eventId };
            Terapkan(sesi, dto);
            return sesi;
        }).ToList();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Schedules.AddRange(semua);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var ids = semua.Select(s => s.Id).ToList();
        return await Sesi(eventId)
            .Where(s => ids.Contains(s.Id))
            .OrderBy(s => s.Tanggal)
            .ThenBy(s => s.WaktuMulai)
            .Select(KeDto)
            .ToListAsync(ct);
    }

    public async Task<ScheduleDto> UpdateAsync(Guid eventId, Guid id, UpdateScheduleDto dto, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(dto, ct);
        var ev = await akses.UbahAsync(eventId, ct);
        var sesi = await CariAsync(eventId, id, ct);
        // Dicek hanya jika berubah, supaya sesi lama tetap bisa diedit walau PIC-nya sudah tidak ditugaskan.
        if (dto.PicCrewId != sesi.PicCrewId) await akses.CekPenanggungJawabAsync(ev, [dto.PicCrewId], ct);

        Terapkan(sesi, dto);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(eventId, id, ct);
    }

    public async Task DeleteAsync(Guid eventId, Guid id, CancellationToken ct = default)
    {
        await akses.UbahAsync(eventId, ct);
        var sesi = await CariAsync(eventId, id, ct);

        db.Schedules.Remove(sesi);
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<Schedule> Sesi(Guid eventId) =>
        db.Schedules.AsNoTracking().Where(s => s.EventId == eventId);

    private async Task<Schedule> CariAsync(Guid eventId, Guid id, CancellationToken ct) =>
        await db.Schedules.FirstOrDefaultAsync(s => s.Id == id && s.EventId == eventId, ct)
        ?? throw new NotFoundException("Sesi rundown tidak ditemukan.");

    private static void Terapkan(Schedule sesi, IScheduleInput dto)
    {
        sesi.Tanggal = dto.Tanggal!.Value;
        sesi.WaktuMulai = dto.WaktuMulai!.Value;
        sesi.WaktuSelesai = dto.WaktuSelesai;
        sesi.Kegiatan = Teks.Wajib(dto.Kegiatan);
        sesi.Lokasi = Teks.Bersihkan(dto.Lokasi);
        sesi.PicCrewId = dto.PicCrewId;
    }
}
