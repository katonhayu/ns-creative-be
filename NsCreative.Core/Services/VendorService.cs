using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Vendors;
using NsCreative.Core.Entities;
using NsCreative.Core.Enums;
using NsCreative.Core.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace NsCreative.Core.Services;

public interface IVendorService
{
    Task<PagedResult<VendorListDto>> GetListAsync(QueryParams query, string? jenisJasa, CancellationToken ct = default);
    Task<VendorDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<VendorLookupDto>> GetLookupAsync(string? search, string? jenisJasa, CancellationToken ct = default);
    Task<VendorRiwayatDto> GetEventsAsync(Guid id, QueryParams query, CancellationToken ct = default);
    Task<IReadOnlyList<VendorBentrokDto>> GetConflictsAsync(
        Guid id, DateOnly? tanggalMulai, DateOnly? tanggalSelesai, Guid? kecualiEventId, CancellationToken ct = default);
    Task<VendorDto> CreateAsync(CreateVendorDto dto, CancellationToken ct = default);
    Task<VendorDto> UpdateAsync(Guid id, UpdateVendorDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class VendorService(
    IAppDbContext db,
    IValidator<CreateVendorDto> createValidator,
    IValidator<UpdateVendorDto> updateValidator) : IVendorService
{
    private static readonly PetaUrutan<Vendor> Urutan = new PetaUrutan<Vendor>()
        .Kolom("nama", v => v.Nama, bawaan: true)
        .Kolom("jenisJasa", v => v.JenisJasa, lanjutan: v => v.Nama)
        .Kolom("createdAt", v => v.CreatedAt);

    public async Task<PagedResult<VendorListDto>> GetListAsync(
        QueryParams query, string? jenisJasa, CancellationToken ct = default) =>
        await FilterJenis(db.Vendors.AsNoTracking(), jenisJasa)
            .Cari(query.Search, v => v.Nama, v => v.JenisJasa, v => v.ContactPerson)
            .Urutkan(query, Urutan)
            .Select(v => new VendorListDto(
                v.Id, v.Nama, v.JenisJasa, v.ContactPerson, v.Telepon,
                db.EventVendors.Where(ev => ev.VendorId == v.Id).Select(ev => ev.EventId).Distinct().Count()))
            .ToPagedResultAsync(query, ct);

    public async Task<VendorDto> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Vendors.AsNoTracking()
            .Where(v => v.Id == id)
            .Select(v => new VendorDto(
                v.Id, v.Nama, v.JenisJasa, v.ContactPerson, v.Telepon,
                db.EventVendors.Where(ev => ev.VendorId == v.Id).Select(ev => ev.EventId).Distinct().Count(),
                v.CreatedAt, v.UpdatedAt))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Vendor tidak ditemukan.");

    public async Task<IReadOnlyList<VendorLookupDto>> GetLookupAsync(
        string? search, string? jenisJasa, CancellationToken ct = default) =>
        await FilterJenis(db.Vendors.AsNoTracking(), jenisJasa)
            .Cari(search, v => v.Nama, v => v.JenisJasa)
            .OrderBy(v => v.Nama)
            .Take(QueryParams.LookupMaks)
            .Select(v => new VendorLookupDto(v.Id, v.Nama, v.JenisJasa))
            .ToListAsync(ct);

    public async Task<VendorRiwayatDto> GetEventsAsync(Guid id, QueryParams query, CancellationToken ct = default)
    {
        // Riwayat tetap terbuka walau vendor sudah dihapus.
        if (!await db.Vendors.IgnoreQueryFilters().AnyAsync(v => v.Id == id, ct))
            throw new NotFoundException("Vendor tidak ditemukan.");

        var semua = db.EventVendors.AsNoTracking().IgnoreQueryFilters().Where(ev => ev.VendorId == id);

        // Total dihitung dari seluruh transaksi (bukan hanya halaman ini), kecuali event yang dibatalkan.
        var total = await semua
            .Where(ev => ev.Event.Status != EventStatus.Dibatalkan)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Transaksi = g.Sum(ev => ev.Biaya),
                BelumLunas = g.Where(ev => ev.StatusPembayaran != StatusPembayaran.Lunas).Sum(ev => ev.Biaya),
                Jumlah = g.Count(),
            })
            .FirstOrDefaultAsync(ct);

        var riwayat = await semua
            .Cari(query.Search, ev => ev.Event.Nama, ev => ev.Kebutuhan)
            .OrderByDescending(ev => ev.Event.TanggalMulai)
            .Select(ev => new VendorEventDto(
                ev.Id, ev.EventId, ev.Event.Nama, ev.Event.TanggalMulai, ev.Event.TanggalSelesai,
                ev.Event.Status, ev.Kebutuhan, ev.Biaya, ev.StatusPembayaran))
            .ToPagedResultAsync(query, ct);

        return new VendorRiwayatDto(total?.Transaksi ?? 0, total?.BelumLunas ?? 0, total?.Jumlah ?? 0, riwayat);
    }

    /// <summary>Transaksi vendor ini di event lain (kecuali yang dibatalkan) yang tanggalnya beririsan.</summary>
    public async Task<IReadOnlyList<VendorBentrokDto>> GetConflictsAsync(
        Guid id, DateOnly? tanggalMulai, DateOnly? tanggalSelesai, Guid? kecualiEventId, CancellationToken ct = default)
    {
        var (dari, sampai) = RentangBentrok.Validasi(tanggalMulai, tanggalSelesai);
        if (!await db.Vendors.AnyAsync(v => v.Id == id, ct))
            throw new NotFoundException("Vendor tidak ditemukan.");

        return await db.EventVendors.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(ev => ev.VendorId == id
                && ev.EventId != kecualiEventId
                && ev.Event.Status != EventStatus.Dibatalkan
                && ev.Event.TanggalMulai <= sampai
                && (ev.Event.TanggalSelesai ?? ev.Event.TanggalMulai) >= dari)
            .OrderBy(ev => ev.Event.TanggalMulai)
            .Select(ev => new VendorBentrokDto(
                ev.EventId, ev.Event.Nama, ev.Event.TanggalMulai, ev.Event.TanggalSelesai, ev.Event.Status, ev.Kebutuhan))
            .ToListAsync(ct);
    }

    public async Task<VendorDto> CreateAsync(CreateVendorDto dto, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(dto, ct);

        var vendor = new Vendor();
        Terapkan(vendor, dto);
        db.Vendors.Add(vendor);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(vendor.Id, ct);
    }

    public async Task<VendorDto> UpdateAsync(Guid id, UpdateVendorDto dto, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(dto, ct);

        var vendor = await db.Vendors.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException("Vendor tidak ditemukan.");
        Terapkan(vendor, dto);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(vendor.Id, ct);
    }

    /// <summary>Soft delete. Transaksi dan budget di event lama tetap tersimpan.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var vendor = await db.Vendors.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException("Vendor tidak ditemukan.");
        vendor.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static IQueryable<Vendor> FilterJenis(IQueryable<Vendor> q, string? jenisJasa)
    {
        var jenis = Teks.Bersihkan(jenisJasa)?.ToLower();
        return jenis is null ? q : q.Where(v => v.JenisJasa.ToLower() == jenis);
    }

    private static void Terapkan(Vendor vendor, IVendorInput dto)
    {
        vendor.Nama = Teks.Wajib(dto.Nama);
        vendor.JenisJasa = Teks.Wajib(dto.JenisJasa);
        vendor.ContactPerson = Teks.Bersihkan(dto.ContactPerson);
        vendor.Telepon = Teks.Bersihkan(dto.Telepon);
    }
}
