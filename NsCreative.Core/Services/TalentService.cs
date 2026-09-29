using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Talents;
using NsCreative.Core.Entities;
using NsCreative.Core.Enums;
using NsCreative.Core.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace NsCreative.Core.Services;

public interface ITalentService
{
    Task<PagedResult<TalentListDto>> GetListAsync(QueryParams query, string? jenis, CancellationToken ct = default);
    Task<TalentDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TalentLookupDto>> GetLookupAsync(string? search, CancellationToken ct = default);
    Task<PagedResult<TalentEventDto>> GetEventsAsync(Guid id, QueryParams query, CancellationToken ct = default);
    Task<IReadOnlyList<TalentBentrokDto>> GetConflictsAsync(
        Guid id, DateOnly? tanggalMulai, DateOnly? tanggalSelesai, Guid? kecualiEventId, CancellationToken ct = default);
    Task<TalentDto> CreateAsync(CreateTalentDto dto, CancellationToken ct = default);
    Task<TalentDto> UpdateAsync(Guid id, UpdateTalentDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class TalentService(
    IAppDbContext db,
    IValidator<CreateTalentDto> createValidator,
    IValidator<UpdateTalentDto> updateValidator) : ITalentService
{
    private static readonly PetaUrutan<Talent> Urutan = new PetaUrutan<Talent>()
        .Kolom("nama", t => t.Nama, bawaan: true)
        .Kolom("jenis", t => t.Jenis, lanjutan: t => t.Nama)
        .Kolom("feeStandar", t => t.FeeStandar, lanjutan: t => t.Nama)
        .Kolom("createdAt", t => t.CreatedAt);

    public async Task<PagedResult<TalentListDto>> GetListAsync(
        QueryParams query, string? jenis, CancellationToken ct = default)
    {
        var q = db.Talents.AsNoTracking()
            .Cari(query.Search, t => t.Nama, t => t.Jenis, t => t.ContactPerson);

        var filterJenis = Teks.Bersihkan(jenis)?.ToLower();
        if (filterJenis is not null) q = q.Where(t => t.Jenis.ToLower() == filterJenis);

        return await q
            .Urutkan(query, Urutan)
            .Select(t => new TalentListDto(
                t.Id, t.Nama, t.Jenis, t.ContactPerson, t.Telepon, t.FeeStandar, t.RiderDefault,
                db.EventTalents.Count(et => et.TalentId == t.Id)))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<TalentDto> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Talents.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TalentDto(
                t.Id, t.Nama, t.Jenis, t.ContactPerson, t.Telepon, t.FeeStandar, t.RiderDefault,
                db.EventTalents.Count(et => et.TalentId == t.Id),
                t.CreatedAt, t.UpdatedAt))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Talent tidak ditemukan.");

    public async Task<IReadOnlyList<TalentLookupDto>> GetLookupAsync(string? search, CancellationToken ct = default) =>
        await db.Talents.AsNoTracking()
            .Cari(search, t => t.Nama, t => t.Jenis)
            .OrderBy(t => t.Nama)
            .Take(QueryParams.LookupMaks)
            .Select(t => new TalentLookupDto(t.Id, t.Nama, t.Jenis, t.FeeStandar, t.RiderDefault))
            .ToListAsync(ct);

    public async Task<PagedResult<TalentEventDto>> GetEventsAsync(Guid id, QueryParams query, CancellationToken ct = default)
    {
        // Riwayat tetap terbuka walau talent sudah dihapus: penugasan lama tidak ikut hilang.
        if (!await db.Talents.IgnoreQueryFilters().AnyAsync(t => t.Id == id, ct))
            throw new NotFoundException("Talent tidak ditemukan.");

        return await db.EventTalents.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(et => et.TalentId == id)
            .Cari(query.Search, et => et.Event.Nama)
            .OrderByDescending(et => et.Event.TanggalMulai)
            .Select(et => new TalentEventDto(
                et.Id, et.EventId, et.Event.Nama, et.Event.TanggalMulai, et.Event.TanggalSelesai,
                et.Event.Status, et.FeeDeal, et.JadwalTampil, et.DurasiMenit, et.Status))
            .ToPagedResultAsync(query, ct);
    }

    /// <summary>
    /// Penugasan talent ini di event lain yang tanggalnya beririsan dengan rentang yang diminta.
    /// Event dibatalkan dan penugasan berstatus batal tidak dihitung.
    /// </summary>
    public async Task<IReadOnlyList<TalentBentrokDto>> GetConflictsAsync(
        Guid id, DateOnly? tanggalMulai, DateOnly? tanggalSelesai, Guid? kecualiEventId, CancellationToken ct = default)
    {
        var (dari, sampai) = RentangBentrok.Validasi(tanggalMulai, tanggalSelesai);
        if (!await db.Talents.AnyAsync(t => t.Id == id, ct))
            throw new NotFoundException("Talent tidak ditemukan.");

        return await db.EventTalents.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(et => et.TalentId == id
                && et.EventId != kecualiEventId
                && et.Status != TalentStatus.Batal
                && et.Event.Status != EventStatus.Dibatalkan
                && et.Event.TanggalMulai <= sampai
                && (et.Event.TanggalSelesai ?? et.Event.TanggalMulai) >= dari)
            .OrderBy(et => et.Event.TanggalMulai)
            .Select(et => new TalentBentrokDto(
                et.EventId, et.Event.Nama, et.Event.TanggalMulai, et.Event.TanggalSelesai,
                et.Event.Status, et.JadwalTampil, et.Status))
            .ToListAsync(ct);
    }

    public async Task<TalentDto> CreateAsync(CreateTalentDto dto, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(dto, ct);

        var talent = new Talent();
        Terapkan(talent, dto);
        db.Talents.Add(talent);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(talent.Id, ct);
    }

    public async Task<TalentDto> UpdateAsync(Guid id, UpdateTalentDto dto, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(dto, ct);

        var talent = await db.Talents.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Talent tidak ditemukan.");
        Terapkan(talent, dto);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(talent.Id, ct);
    }

    /// <summary>Soft delete. Penugasan dan fee deal di event lama tetap tersimpan.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var talent = await db.Talents.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Talent tidak ditemukan.");
        talent.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static void Terapkan(Talent talent, ITalentInput dto)
    {
        talent.Nama = Teks.Wajib(dto.Nama);
        talent.Jenis = Teks.Wajib(dto.Jenis);
        talent.ContactPerson = Teks.Bersihkan(dto.ContactPerson);
        talent.Telepon = Teks.Bersihkan(dto.Telepon);
        talent.FeeStandar = dto.FeeStandar;
        talent.RiderDefault = Teks.Bersihkan(dto.RiderDefault);
    }
}
