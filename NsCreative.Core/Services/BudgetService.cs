using System.Linq.Expressions;
using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Budgets;
using NsCreative.Core.Entities;
using NsCreative.Core.Enums;
using NsCreative.Core.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace NsCreative.Core.Services;

public interface IBudgetService
{
    Task<PagedResult<BudgetDto>> GetListAsync(Guid eventId, QueryParams query, BudgetFilter filter, CancellationToken ct = default);
    Task<BudgetSummaryDto> GetSummaryAsync(Guid eventId, CancellationToken ct = default);
    Task<BudgetDto> GetByIdAsync(Guid eventId, Guid id, CancellationToken ct = default);
    Task<BudgetDto> CreateAsync(Guid eventId, CreateBudgetDto dto, CancellationToken ct = default);
    Task<BudgetDto> UpdateAsync(Guid eventId, Guid id, UpdateBudgetDto dto, CancellationToken ct = default);
    Task<BudgetDto> UpdateRealisasiAsync(Guid eventId, Guid id, UpdateRealisasiDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid eventId, Guid id, CancellationToken ct = default);
}

/// <summary>
/// Anggaran event. Pos manual dikelola penuh di sini. Pos otomatis (SumberTipe terisi) dibuat,
/// diubah, dan dihapus lewat penugasan talent/vendor; di sini hanya realisasinya yang boleh dicatat.
/// </summary>
public class BudgetService(
    IAppDbContext db,
    IAksesEvent akses,
    IValidator<CreateBudgetDto> createValidator,
    IValidator<UpdateBudgetDto> updateValidator,
    IValidator<UpdateRealisasiDto> realisasiValidator) : IBudgetService
{
    private static readonly PetaUrutan<Budget> Urutan = new PetaUrutan<Budget>()
        .Kolom("kategori", b => b.Kategori, bawaan: true, lanjutan: b => b.NamaKebutuhan)
        .Kolom("namaKebutuhan", b => b.NamaKebutuhan)
        .Kolom("estimasiBiaya", b => b.EstimasiBiaya, lanjutan: b => b.NamaKebutuhan)
        .Kolom("biayaAktual", b => b.BiayaAktual, lanjutan: b => b.NamaKebutuhan)
        .Kolom("selisih", b => b.EstimasiBiaya - b.BiayaAktual, lanjutan: b => b.NamaKebutuhan)
        // Nilai tersimpan belum_bayar, dp, lunas: urutan abjadnya sama dengan alur pembayaran.
        .Kolom("statusPembayaran", b => b.StatusPembayaran, lanjutan: b => b.NamaKebutuhan)
        .Kolom("createdAt", b => b.CreatedAt);

    private static readonly Expression<Func<Budget, BudgetDto>> KeDto = b => new BudgetDto(
        b.Id, b.EventId, b.NamaKebutuhan, b.Kategori, b.EstimasiBiaya, b.BiayaAktual, b.Sponsorship,
        b.EstimasiBiaya - b.BiayaAktual, b.StatusPembayaran, b.SumberTipe, b.SumberId, b.SumberTipe != null,
        b.CreatedAt, b.UpdatedAt);

    public async Task<PagedResult<BudgetDto>> GetListAsync(
        Guid eventId, QueryParams query, BudgetFilter filter, CancellationToken ct = default)
    {
        await akses.LihatAsync(eventId, ct);

        var q = Pos(eventId);
        if (Teks.Bersihkan(filter.Kategori)?.ToLower() is { } kategori) q = q.Where(b => b.Kategori.ToLower() == kategori);
        if (filter.StatusPembayaran is { } status) q = q.Where(b => b.StatusPembayaran == status);

        return await q
            .Cari(query.Search, b => b.NamaKebutuhan, b => b.Kategori)
            .Urutkan(query, Urutan)
            .Select(KeDto)
            .ToPagedResultAsync(query, ct);
    }

    public async Task<BudgetSummaryDto> GetSummaryAsync(Guid eventId, CancellationToken ct = default)
    {
        await akses.LihatAsync(eventId, ct);

        var perKategori = await Pos(eventId)
            .GroupBy(b => b.Kategori)
            .Select(g => new
            {
                Kategori = g.Key,
                Estimasi = g.Sum(b => b.EstimasiBiaya),
                Aktual = g.Sum(b => b.BiayaAktual),
                Sponsorship = g.Sum(b => b.Sponsorship),
            })
            .OrderBy(x => x.Kategori)
            .ToListAsync(ct);

        var estimasi = perKategori.Sum(x => x.Estimasi);
        var aktual = perKategori.Sum(x => x.Aktual);
        var sponsorship = perKategori.Sum(x => x.Sponsorship);
        return new BudgetSummaryDto(
            perKategori.Select(x => new BudgetKategoriDto(x.Kategori, x.Estimasi, x.Aktual, x.Estimasi - x.Aktual)).ToList(),
            new BudgetGrandTotalDto(estimasi, aktual, estimasi - aktual, sponsorship, aktual - sponsorship));
    }

    public async Task<BudgetDto> GetByIdAsync(Guid eventId, Guid id, CancellationToken ct = default)
    {
        await akses.LihatAsync(eventId, ct);
        return await Pos(eventId).Where(b => b.Id == id).Select(KeDto).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Pos budget tidak ditemukan.");
    }

    /// <summary>Hanya pos manual: SumberTipe dan SumberId dipaksa null.</summary>
    public async Task<BudgetDto> CreateAsync(Guid eventId, CreateBudgetDto dto, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(dto, ct);
        await akses.UbahAsync(eventId, ct);

        var budget = new Budget { EventId = eventId, SumberTipe = null, SumberId = null };
        Terapkan(budget, dto);
        db.Budgets.Add(budget);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(eventId, budget.Id, ct);
    }

    public async Task<BudgetDto> UpdateAsync(Guid eventId, Guid id, UpdateBudgetDto dto, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(dto, ct);
        await akses.UbahAsync(eventId, ct);
        var budget = await CariAsync(eventId, id, ct);
        if (budget.SumberTipe is not null)
            throw new BusinessRuleException(
                $"Pos {budget.NamaKebutuhan} dibuat otomatis dari {AsalPos(budget)}. " +
                $"Ubah nilainya di tab {TabAsal(budget)}; di sini hanya realisasi yang bisa dicatat.");

        Terapkan(budget, dto);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(eventId, id, ct);
    }

    /// <summary>
    /// ⚡ Catat realisasi. Boleh untuk semua pos, termasuk yang terkunci. Untuk pos dari vendor,
    /// status pembayaran di event_vendors ikut diubah supaya keduanya tidak pernah berbeda.
    /// </summary>
    public async Task<BudgetDto> UpdateRealisasiAsync(
        Guid eventId, Guid id, UpdateRealisasiDto dto, CancellationToken ct = default)
    {
        await realisasiValidator.ValidateAndThrowAsync(dto, ct);
        await akses.UbahAsync(eventId, ct);
        var budget = await CariAsync(eventId, id, ct);
        var vendor = budget.SumberTipe == Budget.SumberEventVendor
            ? await db.EventVendors.FirstOrDefaultAsync(ev => ev.Id == budget.SumberId, ct)
            : null;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        budget.BiayaAktual = dto.BiayaAktual!.Value;
        budget.StatusPembayaran = dto.StatusPembayaran!.Value;
        if (dto.Sponsorship is { } sponsorship) budget.Sponsorship = sponsorship;
        if (vendor is not null) vendor.StatusPembayaran = budget.StatusPembayaran;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetByIdAsync(eventId, id, ct);
    }

    public async Task DeleteAsync(Guid eventId, Guid id, CancellationToken ct = default)
    {
        await akses.UbahAsync(eventId, ct);
        var budget = await CariAsync(eventId, id, ct);
        if (budget.SumberTipe is not null)
            throw new BusinessRuleException(
                $"Pos {budget.NamaKebutuhan} dibuat otomatis dari {AsalPos(budget)}. " +
                $"Hapus penugasannya di tab {TabAsal(budget)}, pos ini akan ikut terhapus.");

        db.Budgets.Remove(budget);
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<Budget> Pos(Guid eventId) =>
        db.Budgets.AsNoTracking().Where(b => b.EventId == eventId);

    private async Task<Budget> CariAsync(Guid eventId, Guid id, CancellationToken ct) =>
        await db.Budgets.FirstOrDefaultAsync(b => b.Id == id && b.EventId == eventId, ct)
        ?? throw new NotFoundException("Pos budget tidak ditemukan.");

    private static string AsalPos(Budget b) =>
        b.SumberTipe == Budget.SumberEventTalent ? "penugasan talent" : "transaksi vendor";

    private static string TabAsal(Budget b) =>
        b.SumberTipe == Budget.SumberEventTalent ? "Talent" : "Vendor";

    private static void Terapkan(Budget budget, IBudgetInput dto)
    {
        budget.NamaKebutuhan = Teks.Wajib(dto.NamaKebutuhan);
        budget.Kategori = Teks.Wajib(dto.Kategori);
        budget.EstimasiBiaya = dto.EstimasiBiaya;
        budget.BiayaAktual = dto.BiayaAktual;
        budget.Sponsorship = dto.Sponsorship;
        budget.StatusPembayaran = dto.StatusPembayaran ?? StatusPembayaran.BelumBayar;
    }
}
