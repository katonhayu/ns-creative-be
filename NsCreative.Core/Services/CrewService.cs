using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Crews;
using NsCreative.Core.Entities;
using NsCreative.Core.Enums;
using NsCreative.Core.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace NsCreative.Core.Services;

public interface ICrewService
{
    Task<PagedResult<CrewListDto>> GetListAsync(QueryParams query, string? keahlian, CancellationToken ct = default);
    Task<CrewDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CrewLookupDto>> GetLookupAsync(string? search, string? keahlian, CancellationToken ct = default);
    Task<PagedResult<CrewEventDto>> GetEventsAsync(Guid id, QueryParams query, CancellationToken ct = default);
    Task<IReadOnlyList<CrewBentrokDto>> GetConflictsAsync(
        Guid id, DateOnly? tanggalMulai, DateOnly? tanggalSelesai, Guid? kecualiEventId, CancellationToken ct = default);
    Task<CrewDto> CreateAsync(CreateCrewDto dto, CancellationToken ct = default);
    Task<CrewDto> UpdateAsync(Guid id, UpdateCrewDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class CrewService(
    IAppDbContext db,
    IValidator<CreateCrewDto> createValidator,
    IValidator<UpdateCrewDto> updateValidator) : ICrewService
{
    private static readonly PetaUrutan<Crew> Urutan = new PetaUrutan<Crew>()
        .Kolom("nama", c => c.Nama, bawaan: true)
        .Kolom("keahlian", c => c.Keahlian, nullDiBawah: true, lanjutan: c => c.Nama)
        .Kolom("createdAt", c => c.CreatedAt);

    public async Task<PagedResult<CrewListDto>> GetListAsync(
        QueryParams query, string? keahlian, CancellationToken ct = default) =>
        await FilterKeahlian(db.Crews.AsNoTracking(), keahlian)
            .Cari(query.Search, c => c.Nama, c => c.Keahlian, c => c.Telepon)
            .Urutkan(query, Urutan)
            .Select(c => new CrewListDto(
                c.Id, c.UserId, c.User != null ? c.User.Email : null, c.Nama, c.Telepon, c.Keahlian,
                db.EventCrews.Where(ec => ec.CrewId == c.Id).Select(ec => ec.EventId).Distinct().Count()))
            .ToPagedResultAsync(query, ct);

    public async Task<CrewDto> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Crews.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CrewDto(
                c.Id, c.UserId, c.User != null ? c.User.Email : null, c.Nama, c.Telepon, c.Keahlian,
                db.EventCrews.Where(ec => ec.CrewId == c.Id).Select(ec => ec.EventId).Distinct().Count(),
                c.CreatedAt, c.UpdatedAt))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Crew tidak ditemukan.");

    public async Task<IReadOnlyList<CrewLookupDto>> GetLookupAsync(
        string? search, string? keahlian, CancellationToken ct = default) =>
        await FilterKeahlian(db.Crews.AsNoTracking(), keahlian)
            .Cari(search, c => c.Nama, c => c.Keahlian)
            .OrderBy(c => c.Nama)
            .Take(QueryParams.LookupMaks)
            .Select(c => new CrewLookupDto(c.Id, c.Nama, c.Keahlian))
            .ToListAsync(ct);

    public async Task<PagedResult<CrewEventDto>> GetEventsAsync(Guid id, QueryParams query, CancellationToken ct = default)
    {
        // Riwayat tetap terbuka walau crew sudah dihapus.
        if (!await db.Crews.IgnoreQueryFilters().AnyAsync(c => c.Id == id, ct))
            throw new NotFoundException("Crew tidak ditemukan.");

        return await db.EventCrews.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(ec => ec.CrewId == id)
            .Cari(query.Search, ec => ec.Event.Nama, ec => ec.Posisi)
            .OrderByDescending(ec => ec.Event.TanggalMulai)
            .Select(ec => new CrewEventDto(
                ec.Id, ec.EventId, ec.Event.Nama, ec.Event.TanggalMulai, ec.Event.TanggalSelesai,
                ec.Event.Status, ec.Posisi, ec.TanggalBertugas))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<IReadOnlyList<CrewBentrokDto>> GetConflictsAsync(
        Guid id, DateOnly? tanggalMulai, DateOnly? tanggalSelesai, Guid? kecualiEventId, CancellationToken ct = default)
    {
        var (dari, sampai) = RentangBentrok.Validasi(tanggalMulai, tanggalSelesai);
        if (!await db.Crews.AnyAsync(c => c.Id == id, ct))
            throw new NotFoundException("Crew tidak ditemukan.");

        var bentrok = await BentrokCrew.CariAsync(db, [new BentrokCrew.Rentang(id, dari, sampai)], kecualiEventId, ct);
        return bentrok[id].ToList();
    }

    public async Task<CrewDto> CreateAsync(CreateCrewDto dto, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(dto, ct);
        await CekAkunAsync(dto.UserId, null, ct);

        var crew = new Crew();
        Terapkan(crew, dto);
        db.Crews.Add(crew);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(crew.Id, ct);
    }

    public async Task<CrewDto> UpdateAsync(Guid id, UpdateCrewDto dto, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(dto, ct);

        var crew = await db.Crews.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Crew tidak ditemukan.");
        // Dicek hanya jika tautannya berubah, supaya crew dengan akun yang kini nonaktif tetap bisa diedit.
        if (dto.UserId != crew.UserId) await CekAkunAsync(dto.UserId, id, ct);
        Terapkan(crew, dto);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(crew.Id, ct);
    }

    /// <summary>Soft delete. Penugasan di event lama tetap tersimpan.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var crew = await db.Crews.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Crew tidak ditemukan.");
        crew.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Akun yang ditautkan harus ada, aktif, berperan crew, dan belum dipakai crew lain.</summary>
    private async Task CekAkunAsync(Guid? userId, Guid? kecualiCrewId, CancellationToken ct)
    {
        if (userId is null) return;

        var akun = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Email, u.Role, u.IsAktif })
            .FirstOrDefaultAsync(ct)
            ?? throw new BusinessRuleException("Akun login yang dipilih tidak ditemukan.");
        if (akun.Role != UserRole.Crew)
            throw new BusinessRuleException($"Akun {akun.Email} bukan akun berperan crew.");
        if (!akun.IsAktif)
            throw new BusinessRuleException($"Akun {akun.Email} sedang nonaktif.");

        var dipakai = await db.Crews.AsNoTracking()
            .Where(c => c.UserId == userId && c.Id != kecualiCrewId)
            .Select(c => c.Nama)
            .FirstOrDefaultAsync(ct);
        if (dipakai is not null)
            throw new BusinessRuleException($"Akun {akun.Email} sudah ditautkan ke crew {dipakai}.");
    }

    private static IQueryable<Crew> FilterKeahlian(IQueryable<Crew> q, string? keahlian)
    {
        // Keahlian berupa teks bebas, jadi filternya "mengandung", bukan sama persis.
        var kata = Teks.Bersihkan(keahlian)?.ToLower();
        return kata is null ? q : q.Where(c => c.Keahlian != null && c.Keahlian.ToLower().Contains(kata));
    }

    private static void Terapkan(Crew crew, ICrewInput dto)
    {
        crew.UserId = dto.UserId;
        crew.Nama = Teks.Wajib(dto.Nama);
        crew.Telepon = Teks.Bersihkan(dto.Telepon);
        crew.Keahlian = Teks.Bersihkan(dto.Keahlian);
    }
}
