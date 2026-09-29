using NsCreative.Core.Common;
using NsCreative.Core.Dtos.EventTalents;
using NsCreative.Core.Entities;
using NsCreative.Core.Enums;
using NsCreative.Core.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace NsCreative.Core.Services;

public interface IEventTalentService
{
    Task<PagedResult<EventTalentDto>> GetListAsync(Guid eventId, QueryParams query, CancellationToken ct = default);
    Task<EventTalentDto> GetByIdAsync(Guid eventId, Guid id, CancellationToken ct = default);
    Task<EventTalentDto> CreateAsync(Guid eventId, CreateEventTalentDto dto, CancellationToken ct = default);
    Task<EventTalentDto> UpdateAsync(Guid eventId, Guid id, UpdateEventTalentDto dto, CancellationToken ct = default);
    Task<EventTalentDto> UpdateStatusAsync(Guid eventId, Guid id, UpdateEventTalentStatusDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid eventId, Guid id, CancellationToken ct = default);
}

/// <summary>
/// Penugasan talent ke event. Setiap penugasan punya tepat satu baris budget
/// (SumberTipe = event_talent), dibuat, disinkronkan, dan dihapus dalam transaksi yang sama.
/// </summary>
public class EventTalentService(
    IAppDbContext db,
    IAksesEvent akses,
    IValidator<CreateEventTalentDto> createValidator,
    IValidator<UpdateEventTalentDto> updateValidator,
    IValidator<UpdateEventTalentStatusDto> statusValidator) : IEventTalentService
{
    private static readonly PetaUrutan<EventTalent> Urutan = new PetaUrutan<EventTalent>()
        .Kolom("jadwalTampil", et => et.JadwalTampil, bawaan: true, nullDiBawah: true, lanjutan: et => et.Talent.Nama)
        .Kolom("nama", et => et.Talent.Nama)
        .Kolom("feeDeal", et => et.FeeDeal, lanjutan: et => et.Talent.Nama)
        // Mengikuti alur negosiasi, bukan urutan abjad.
        .Kolom("status", et =>
            et.Status == TalentStatus.Dihubungi ? 0 :
            et.Status == TalentStatus.Deal ? 1 :
            et.Status == TalentStatus.Kontrak ? 2 :
            et.Status == TalentStatus.Selesai ? 3 : 4, lanjutan: et => et.Talent.Nama)
        .Kolom("createdAt", et => et.CreatedAt);

    public async Task<PagedResult<EventTalentDto>> GetListAsync(
        Guid eventId, QueryParams query, CancellationToken ct = default)
    {
        await akses.LihatAsync(eventId, ct);
        return await Proyeksi(Penugasan(eventId)
                .Cari(query.Search, et => et.Talent.Nama, et => et.Talent.Jenis)
                .Urutkan(query, Urutan))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<EventTalentDto> GetByIdAsync(Guid eventId, Guid id, CancellationToken ct = default)
    {
        await akses.LihatAsync(eventId, ct);
        return await Proyeksi(Penugasan(eventId).Where(et => et.Id == id)).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Penugasan talent tidak ditemukan.");
    }

    /// <summary>⚡ Buat EventTalent + baris budget (kategori Talent, estimasi = fee deal) dalam satu transaksi.</summary>
    public async Task<EventTalentDto> CreateAsync(Guid eventId, CreateEventTalentDto dto, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(dto, ct);
        await akses.UbahAsync(eventId, ct);

        var namaTalent = await db.Talents.AsNoTracking()
            .Where(t => t.Id == dto.TalentId)
            .Select(t => t.Nama)
            .FirstOrDefaultAsync(ct)
            ?? throw new BusinessRuleException("Talent yang dipilih tidak ditemukan atau sudah dihapus.");
        if (await db.EventTalents.AnyAsync(et => et.EventId == eventId && et.TalentId == dto.TalentId, ct))
            throw new BusinessRuleException($"{namaTalent} sudah ditugaskan di event ini.");

        // Id dibuat di C#, jadi SumberId budget sudah bisa diisi sebelum disimpan.
        var penugasan = new EventTalent
        {
            EventId = eventId,
            TalentId = dto.TalentId,
            Status = dto.Status ?? TalentStatus.Dihubungi,
        };
        Terapkan(penugasan, dto);
        var budget = new Budget
        {
            EventId = eventId,
            NamaKebutuhan = namaTalent,
            Kategori = Budget.KategoriTalent,
            EstimasiBiaya = penugasan.FeeDeal,
            StatusPembayaran = StatusPembayaran.BelumBayar,
            SumberTipe = Budget.SumberEventTalent,
            SumberId = penugasan.Id,
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.EventTalents.Add(penugasan);
        db.Budgets.Add(budget);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetByIdAsync(eventId, penugasan.Id, ct);
    }

    /// <summary>⚡ Ubah detail penugasan. Estimasi budget ikut fee deal; biaya aktual tidak disentuh.</summary>
    public async Task<EventTalentDto> UpdateAsync(
        Guid eventId, Guid id, UpdateEventTalentDto dto, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(dto, ct);
        await akses.UbahAsync(eventId, ct);
        var penugasan = await CariAsync(eventId, id, ct);
        var budget = await BudgetAsync(id, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        Terapkan(penugasan, dto);
        if (budget is not null) budget.EstimasiBiaya = penugasan.FeeDeal;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetByIdAsync(eventId, id, ct);
    }

    public async Task<EventTalentDto> UpdateStatusAsync(
        Guid eventId, Guid id, UpdateEventTalentStatusDto dto, CancellationToken ct = default)
    {
        await statusValidator.ValidateAndThrowAsync(dto, ct);
        await akses.UbahAsync(eventId, ct);
        var penugasan = await CariAsync(eventId, id, ct);

        penugasan.Status = dto.Status!.Value;
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(eventId, id, ct);
    }

    /// <summary>⚡ Hapus penugasan + budget terkait. Ditolak jika biaya aktualnya sudah dicatat.</summary>
    public async Task DeleteAsync(Guid eventId, Guid id, CancellationToken ct = default)
    {
        await akses.UbahAsync(eventId, ct);
        var penugasan = await CariAsync(eventId, id, ct);
        var budget = await BudgetAsync(id, ct);
        if (budget is { BiayaAktual: > 0 })
            throw new BusinessRuleException(
                $"Biaya aktual untuk {budget.NamaKebutuhan} sudah dicatat di budget. " +
                "Nolkan realisasinya di tab Budget sebelum menghapus penugasan ini.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.EventTalents.Remove(penugasan);
        if (budget is not null) db.Budgets.Remove(budget);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Filter soft delete diabaikan supaya talent yang sudah dihapus tetap tampil di event lama.</summary>
    private IQueryable<EventTalent> Penugasan(Guid eventId) =>
        db.EventTalents.AsNoTracking().IgnoreQueryFilters().Where(et => et.EventId == eventId);

    private IQueryable<EventTalentDto> Proyeksi(IQueryable<EventTalent> q) =>
        q.Select(et => new EventTalentDto(
            et.Id, et.EventId, et.TalentId, et.Talent.Nama, et.Talent.Jenis, et.Talent.FeeStandar,
            et.FeeDeal, et.DurasiMenit, et.JadwalTampil, et.Rider, et.Status,
            db.Budgets
                .Where(b => b.SumberTipe == Budget.SumberEventTalent && b.SumberId == et.Id)
                .Select(b => (Guid?)b.Id)
                .FirstOrDefault(),
            db.Budgets
                .Where(b => b.SumberTipe == Budget.SumberEventTalent && b.SumberId == et.Id)
                .Select(b => b.BiayaAktual)
                .FirstOrDefault(),
            et.CreatedAt, et.UpdatedAt));

    private async Task<EventTalent> CariAsync(Guid eventId, Guid id, CancellationToken ct) =>
        await db.EventTalents.FirstOrDefaultAsync(et => et.Id == id && et.EventId == eventId, ct)
        ?? throw new NotFoundException("Penugasan talent tidak ditemukan.");

    private Task<Budget?> BudgetAsync(Guid eventTalentId, CancellationToken ct) =>
        db.Budgets.FirstOrDefaultAsync(b => b.SumberTipe == Budget.SumberEventTalent && b.SumberId == eventTalentId, ct);

    private static void Terapkan(EventTalent penugasan, IEventTalentInput dto)
    {
        penugasan.FeeDeal = dto.FeeDeal;
        penugasan.DurasiMenit = dto.DurasiMenit;
        penugasan.JadwalTampil = Waktu.KeUtc(dto.JadwalTampil);
        penugasan.Rider = Teks.Bersihkan(dto.Rider);
    }
}
