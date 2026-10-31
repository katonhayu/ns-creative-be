using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Users;
using NsCreative.Core.Entities;
using NsCreative.Core.Enums;
using NsCreative.Core.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace NsCreative.Core.Services;

public interface IUserService
{
    Task<PagedResult<UserListDto>> GetListAsync(QueryParams query, UserRole? role, bool? isAktif, CancellationToken ct = default);
    Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<UserLookupDto>> GetLookupAsync(string? search, IReadOnlyCollection<UserRole>? role, CancellationToken ct = default);
    Task<UserDto> CreateAsync(CreateUserDto dto, CancellationToken ct = default);
    Task<UserDto> UpdateAsync(Guid id, UpdateUserDto dto, CancellationToken ct = default);
    Task<UserDto> UpdateStatusAsync(Guid id, UpdateUserStatusDto dto, CancellationToken ct = default);
    Task ResetPasswordAsync(Guid id, ResetPasswordDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class UserService(
    IAppDbContext db,
    IPasswordHasher<User> hasher,
    IPenggunaSaatIni pengguna,
    IValidator<CreateUserDto> createValidator,
    IValidator<UpdateUserDto> updateValidator,
    IValidator<ResetPasswordDto> resetValidator) : IUserService
{
    private static readonly PetaUrutan<User> Urutan = new PetaUrutan<User>()
        .Kolom("nama", u => u.Nama, bawaan: true)
        .Kolom("email", u => u.Email)
        .Kolom("role", u => u.Role, lanjutan: u => u.Nama)
        .Kolom("createdAt", u => u.CreatedAt);

    public async Task<PagedResult<UserListDto>> GetListAsync(
        QueryParams query, UserRole? role, bool? isAktif, CancellationToken ct = default)
    {
        var q = db.Users.AsNoTracking().Cari(query.Search, u => u.Nama, u => u.Email);
        if (role is not null) q = q.Where(u => u.Role == role);
        if (isAktif is not null) q = q.Where(u => u.IsAktif == isAktif);

        return await q
            .Urutkan(query, Urutan)
            .Select(u => new UserListDto(
                u.Id, u.Nama, u.Email, u.Role, u.IsAktif,
                db.Events.Count(e => e.PicCrew.UserId == u.Id)))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UserDto(
                u.Id, u.Nama, u.Email, u.Role, u.IsAktif,
                db.Events.Count(e => e.PicCrew.UserId == u.Id),
                u.CreatedAt, u.UpdatedAt))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Pengguna tidak ditemukan.");

    public async Task<IReadOnlyList<UserLookupDto>> GetLookupAsync(
        string? search, IReadOnlyCollection<UserRole>? role, CancellationToken ct = default)
    {
        var q = db.Users.AsNoTracking().Where(u => u.IsAktif).Cari(search, u => u.Nama, u => u.Email);
        // Boleh lebih dari satu peran, misalnya dropdown PIC: ?role=admin&role=pic.
        if (role is { Count: > 0 }) q = q.Where(u => role.Contains(u.Role));

        return await q
            .OrderBy(u => u.Nama)
            .Take(QueryParams.LookupMaks)
            .Select(u => new UserLookupDto(u.Id, u.Nama, u.Email, u.Role))
            .ToListAsync(ct);
    }

    public async Task<UserDto> CreateAsync(CreateUserDto dto, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(dto, ct);
        var email = Teks.Email(dto.Email)!;
        await CekEmailUnikAsync(email, null, ct);

        var user = new User { Nama = Teks.Wajib(dto.Nama), Email = email, Role = dto.Role, IsAktif = true };
        user.PasswordHash = hasher.HashPassword(user, dto.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(user.Id, ct);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserDto dto, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(dto, ct);
        var user = await CariAsync(id, ct);
        var email = Teks.Email(dto.Email)!;
        await CekEmailUnikAsync(email, id, ct);
        if (id == pengguna.Id && dto.Role != user.Role)
            throw new BusinessRuleException("Anda tidak bisa mengubah peran akun sendiri. Minta admin lain melakukannya.");

        // Akun yang masih ditautkan ke data crew harus tetap berperan crew.
        if (user.Role == UserRole.Crew && dto.Role != UserRole.Crew)
        {
            var crew = await db.Crews.Where(c => c.UserId == id).Select(c => c.Nama).FirstOrDefaultAsync(ct);
            if (crew is not null)
                throw new BusinessRuleException(
                    $"Akun ini ditautkan ke crew {crew}. Lepas tautannya di data crew sebelum mengubah peran.");
        }

        user.Nama = Teks.Wajib(dto.Nama);
        user.Email = email;
        user.Role = dto.Role;
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<UserDto> UpdateStatusAsync(Guid id, UpdateUserStatusDto dto, CancellationToken ct = default)
    {
        var user = await CariAsync(id, ct);
        if (id == pengguna.Id && !dto.IsAktif)
            throw new BusinessRuleException("Anda tidak bisa menonaktifkan akun sendiri.");

        user.IsAktif = dto.IsAktif;
        // Akun nonaktif langsung keluar dari semua sesi.
        if (!dto.IsAktif) await SesiLogin.CabutSemuaAsync(db, id, ct);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task ResetPasswordAsync(Guid id, ResetPasswordDto dto, CancellationToken ct = default)
    {
        await resetValidator.ValidateAndThrowAsync(dto, ct);
        var user = await CariAsync(id, ct);
        user.PasswordHash = hasher.HashPassword(user, dto.PasswordBaru);
        // Sesi yang sedang berjalan harus login ulang dengan kata sandi baru.
        await SesiLogin.CabutSemuaAsync(db, id, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Hapus permanen (tabel users tidak punya soft delete). PIC event, task, dan rundown tercatat
    /// pada data crew, jadi riwayat tetap utuh; tautan crew ke akun ini dilepas oleh database
    /// (SET NULL). Ditolak jika crew miliknya masih PIC event aktif, karena event itu akan
    /// kehilangan pengelolanya.
    /// </summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await CariAsync(id, ct);
        if (id == pengguna.Id)
            throw new BusinessRuleException("Anda tidak bisa menghapus akun sendiri.");

        var jumlahAktif = await db.Events.IgnoreQueryFilters().CountAsync(
            e => e.PicCrew.UserId == id && EventStatusExtensions.Aktif.Contains(e.Status), ct);
        if (jumlahAktif > 0)
            throw new BusinessRuleException(
                $"{user.Nama} masih menjadi PIC {jumlahAktif} event aktif. Pindahkan PIC-nya terlebih dahulu.");

        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);
    }

    private async Task<User> CariAsync(Guid id, CancellationToken ct) =>
        await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
        ?? throw new NotFoundException("Pengguna tidak ditemukan.");

    private async Task CekEmailUnikAsync(string email, Guid? kecualiId, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.Email == email && u.Id != kecualiId, ct))
            throw new BusinessRuleException($"Email {email} sudah dipakai pengguna lain.");
    }
}
