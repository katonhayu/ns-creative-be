using NsCreative.Core.Common;
using NsCreative.Core.Dtos.EventVendors;
using NsCreative.Core.Entities;
using NsCreative.Core.Enums;
using NsCreative.Core.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace NsCreative.Core.Services;

public interface IEventVendorService
{
    Task<PagedResult<EventVendorDto>> GetListAsync(Guid eventId, QueryParams query, CancellationToken ct = default);
    Task<EventVendorDto> GetByIdAsync(Guid eventId, Guid id, CancellationToken ct = default);
    Task<EventVendorDto> CreateAsync(Guid eventId, CreateEventVendorDto dto, CancellationToken ct = default);
    Task<EventVendorDto> UpdateAsync(Guid eventId, Guid id, UpdateEventVendorDto dto, CancellationToken ct = default);
    Task<EventVendorDto> UpdatePembayaranAsync(Guid eventId, Guid id, UpdateEventVendorPembayaranDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid eventId, Guid id, CancellationToken ct = default);
}

/// <summary>
/// Transaksi vendor di event. Setiap transaksi punya tepat satu baris budget
/// (SumberTipe = event_vendor), dibuat, disinkronkan, dan dihapus dalam transaksi yang sama.
/// </summary>
public class EventVendorService(
    IAppDbContext db,
    IAksesEvent akses,
    IValidator<CreateEventVendorDto> createValidator,
    IValidator<UpdateEventVendorDto> updateValidator,
    IValidator<UpdateEventVendorPembayaranDto> pembayaranValidator) : IEventVendorService
{
    private const int NamaKebutuhanMaks = 150;

    private static readonly PetaUrutan<EventVendor> Urutan = new PetaUrutan<EventVendor>()
        .Kolom("nama", ev => ev.Vendor.Nama, bawaan: true)
        .Kolom("jenisJasa", ev => ev.Vendor.JenisJasa, lanjutan: ev => ev.Vendor.Nama)
        .Kolom("biaya", ev => ev.Biaya, lanjutan: ev => ev.Vendor.Nama)
        // Nilai tersimpan belum_bayar, dp, lunas: urutan abjadnya sama dengan alur pembayaran.
        .Kolom("statusPembayaran", ev => ev.StatusPembayaran, lanjutan: ev => ev.Vendor.Nama)
        .Kolom("createdAt", ev => ev.CreatedAt);

    public async Task<PagedResult<EventVendorDto>> GetListAsync(
        Guid eventId, QueryParams query, CancellationToken ct = default)
    {
        await akses.LihatAsync(eventId, ct);
        return await Proyeksi(Transaksi(eventId)
                .Cari(query.Search, ev => ev.Vendor.Nama, ev => ev.Vendor.JenisJasa, ev => ev.Kebutuhan)
                .Urutkan(query, Urutan))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<EventVendorDto> GetByIdAsync(Guid eventId, Guid id, CancellationToken ct = default)
    {
        await akses.LihatAsync(eventId, ct);
        return await Proyeksi(Transaksi(eventId).Where(ev => ev.Id == id)).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Transaksi vendor tidak ditemukan.");
    }

    /// <summary>⚡ Buat EventVendor + baris budget (kategori Vendor, estimasi = biaya) dalam satu transaksi.</summary>
    public async Task<EventVendorDto> CreateAsync(Guid eventId, CreateEventVendorDto dto, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(dto, ct);
        await akses.UbahAsync(eventId, ct);

        var namaVendor = await db.Vendors.AsNoTracking()
            .Where(v => v.Id == dto.VendorId)
            .Select(v => v.Nama)
            .FirstOrDefaultAsync(ct)
            ?? throw new BusinessRuleException("Vendor yang dipilih tidak ditemukan atau sudah dihapus.");

        // Id dibuat di C#, jadi SumberId budget sudah bisa diisi sebelum disimpan.
        var transaksi = new EventVendor
        {
            EventId = eventId,
            VendorId = dto.VendorId,
            StatusPembayaran = dto.StatusPembayaran ?? StatusPembayaran.BelumBayar,
        };
        Terapkan(transaksi, dto);
        var budget = new Budget
        {
            EventId = eventId,
            NamaKebutuhan = NamaKebutuhan(namaVendor, transaksi.Kebutuhan),
            Kategori = Budget.KategoriVendor,
            EstimasiBiaya = transaksi.Biaya,
            StatusPembayaran = transaksi.StatusPembayaran,
            SumberTipe = Budget.SumberEventVendor,
            SumberId = transaksi.Id,
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.EventVendors.Add(transaksi);
        db.Budgets.Add(budget);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetByIdAsync(eventId, transaksi.Id, ct);
    }

    /// <summary>⚡ Ubah kebutuhan dan biaya. Estimasi dan nama baris budget ikut; biaya aktual tidak disentuh.</summary>
    public async Task<EventVendorDto> UpdateAsync(
        Guid eventId, Guid id, UpdateEventVendorDto dto, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(dto, ct);
        await akses.UbahAsync(eventId, ct);
        var transaksi = await CariAsync(eventId, id, ct);
        var budget = await BudgetAsync(id, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        Terapkan(transaksi, dto);
        if (budget is not null)
        {
            budget.EstimasiBiaya = transaksi.Biaya;
            budget.NamaKebutuhan = NamaKebutuhan(transaksi.Vendor.Nama, transaksi.Kebutuhan);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetByIdAsync(eventId, id, ct);
    }

    /// <summary>⚡ Ubah status pembayaran di EventVendor dan budget terkait sekaligus.</summary>
    public async Task<EventVendorDto> UpdatePembayaranAsync(
        Guid eventId, Guid id, UpdateEventVendorPembayaranDto dto, CancellationToken ct = default)
    {
        await pembayaranValidator.ValidateAndThrowAsync(dto, ct);
        await akses.UbahAsync(eventId, ct);
        var transaksi = await CariAsync(eventId, id, ct);
        var budget = await BudgetAsync(id, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        transaksi.StatusPembayaran = dto.StatusPembayaran!.Value;
        if (budget is not null) budget.StatusPembayaran = transaksi.StatusPembayaran;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetByIdAsync(eventId, id, ct);
    }

    /// <summary>⚡ Hapus transaksi + budget terkait. Ditolak jika biaya aktualnya sudah dicatat.</summary>
    public async Task DeleteAsync(Guid eventId, Guid id, CancellationToken ct = default)
    {
        await akses.UbahAsync(eventId, ct);
        var transaksi = await CariAsync(eventId, id, ct);
        var budget = await BudgetAsync(id, ct);
        if (budget is { BiayaAktual: > 0 })
            throw new BusinessRuleException(
                $"Biaya aktual untuk {budget.NamaKebutuhan} sudah dicatat di budget. " +
                "Nolkan realisasinya di tab Budget sebelum menghapus transaksi ini.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.EventVendors.Remove(transaksi);
        if (budget is not null) db.Budgets.Remove(budget);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Filter soft delete diabaikan supaya vendor yang sudah dihapus tetap tampil di event lama.</summary>
    private IQueryable<EventVendor> Transaksi(Guid eventId) =>
        db.EventVendors.AsNoTracking().IgnoreQueryFilters().Where(ev => ev.EventId == eventId);

    private IQueryable<EventVendorDto> Proyeksi(IQueryable<EventVendor> q) =>
        q.Select(ev => new EventVendorDto(
            ev.Id, ev.EventId, ev.VendorId, ev.Vendor.Nama, ev.Vendor.JenisJasa,
            ev.Kebutuhan, ev.Biaya, ev.StatusPembayaran,
            db.Budgets
                .Where(b => b.SumberTipe == Budget.SumberEventVendor && b.SumberId == ev.Id)
                .Select(b => (Guid?)b.Id)
                .FirstOrDefault(),
            db.Budgets
                .Where(b => b.SumberTipe == Budget.SumberEventVendor && b.SumberId == ev.Id)
                .Select(b => b.BiayaAktual)
                .FirstOrDefault(),
            ev.CreatedAt, ev.UpdatedAt));

    /// <summary>Vendor ikut dimuat (termasuk yang sudah dihapus) karena namanya dipakai untuk baris budget.</summary>
    private async Task<EventVendor> CariAsync(Guid eventId, Guid id, CancellationToken ct) =>
        await db.EventVendors.IgnoreQueryFilters()
            .Include(ev => ev.Vendor)
            .FirstOrDefaultAsync(ev => ev.Id == id && ev.EventId == eventId, ct)
        ?? throw new NotFoundException("Transaksi vendor tidak ditemukan.");

    private Task<Budget?> BudgetAsync(Guid eventVendorId, CancellationToken ct) =>
        db.Budgets.FirstOrDefaultAsync(b => b.SumberTipe == Budget.SumberEventVendor && b.SumberId == eventVendorId, ct);

    /// <summary>
    /// Satu vendor bisa punya beberapa transaksi di event yang sama, jadi nama baris budget
    /// memuat kebutuhannya: "Sinar Audio Pro (Sound system 10.000 watt)".
    /// </summary>
    private static string NamaKebutuhan(string namaVendor, string? kebutuhan)
    {
        var nama = kebutuhan is null ? namaVendor : $"{namaVendor} ({kebutuhan})";
        return nama.Length <= NamaKebutuhanMaks ? nama : string.Concat(nama.AsSpan(0, NamaKebutuhanMaks - 1), "…");
    }

    private static void Terapkan(EventVendor transaksi, IEventVendorInput dto)
    {
        transaksi.Kebutuhan = Teks.Bersihkan(dto.Kebutuhan);
        transaksi.Biaya = dto.Biaya;
    }
}
