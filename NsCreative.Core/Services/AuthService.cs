using System.Security.Cryptography;
using System.Text;
using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Auth;
using NsCreative.Core.Entities;
using NsCreative.Core.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace NsCreative.Core.Services;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default);
    Task<LoginResponseDto> RefreshAsync(RefreshTokenDto dto, CancellationToken ct = default);
    Task LogoutAsync(RefreshTokenDto dto, CancellationToken ct = default);
    Task<AuthUserDto> GetMeAsync(CancellationToken ct = default);
    Task<LoginResponseDto> ChangePasswordAsync(ChangePasswordDto dto, CancellationToken ct = default);
}

/// <summary>
/// Login dengan email + kata sandi, access token JWT berumur pendek, dan refresh token yang
/// diputar setiap dipakai. Refresh token yang sudah dicabut lalu dipakai lagi dianggap bocor:
/// semua sesi akun tersebut ikut dicabut.
/// </summary>
public class AuthService(
    IAppDbContext db,
    IPasswordHasher<User> hasher,
    IPembuatToken pembuatToken,
    IPenggunaSaatIni pengguna,
    IValidator<LoginDto> loginValidator,
    IValidator<RefreshTokenDto> refreshValidator,
    IValidator<ChangePasswordDto> passwordValidator) : IAuthService
{
    private const string PesanLoginGagal = "Email atau kata sandi salah.";
    private const string PesanNonaktif = "Akun Anda sedang nonaktif. Hubungi admin.";

    public async Task<LoginResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        await loginValidator.ValidateAndThrowAsync(dto, ct);

        var email = Teks.Email(dto.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        // Pesan sama untuk email tak dikenal dan kata sandi salah, supaya email terdaftar tidak bisa ditebak.
        if (user is null) throw new UnauthorizedException(PesanLoginGagal);

        var hasil = hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
        if (hasil == PasswordVerificationResult.Failed) throw new UnauthorizedException(PesanLoginGagal);
        if (!user.IsAktif) throw new UnauthorizedException(PesanNonaktif);
        if (hasil == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = hasher.HashPassword(user, dto.Password);

        return await BuatSesiAsync(user, digantikan: null, ct);
    }

    /// <summary>⚡ Tukar refresh token dengan pasangan token baru; token lama langsung dicabut.</summary>
    public async Task<LoginResponseDto> RefreshAsync(RefreshTokenDto dto, CancellationToken ct = default)
    {
        await refreshValidator.ValidateAndThrowAsync(dto, ct);

        var hash = SesiLogin.Hash(dto.RefreshToken);
        var token = await db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
            ?? throw new UnauthorizedException("Sesi tidak valid. Silakan login ulang.");

        if (token.RevokedAt is not null)
        {
            await SesiLogin.CabutSemuaAsync(db, token.UserId, ct);
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException(
                "Sesi ini sudah tidak berlaku. Demi keamanan, semua sesi akun ini diakhiri. Silakan login ulang.");
        }
        if (token.ExpiresAt <= DateTime.UtcNow)
            throw new UnauthorizedException("Sesi sudah berakhir. Silakan login ulang.");
        if (!token.User.IsAktif)
        {
            await SesiLogin.CabutSemuaAsync(db, token.UserId, ct);
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException(PesanNonaktif);
        }

        return await BuatSesiAsync(token.User, digantikan: token, ct);
    }

    /// <summary>Cabut refresh token milik pengguna login. Token yang tidak dikenal diabaikan.</summary>
    public async Task LogoutAsync(RefreshTokenDto dto, CancellationToken ct = default)
    {
        await refreshValidator.ValidateAndThrowAsync(dto, ct);

        var hash = SesiLogin.Hash(dto.RefreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(
            t => t.TokenHash == hash && t.UserId == pengguna.Id && t.RevokedAt == null, ct);
        if (token is null) return;

        token.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<AuthUserDto> GetMeAsync(CancellationToken ct = default)
    {
        var id = pengguna.Id;
        return await db.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new AuthUserDto(
                u.Id, u.Nama, u.Email, u.Role,
                db.Crews.Where(c => c.UserId == u.Id).Select(c => (Guid?)c.Id).FirstOrDefault()))
            .FirstOrDefaultAsync(ct)
            ?? throw new UnauthorizedException("Akun tidak ditemukan. Silakan login ulang.");
    }

    /// <summary>Semua sesi lain dicabut; respons berisi token baru supaya sesi ini tetap berjalan.</summary>
    public async Task<LoginResponseDto> ChangePasswordAsync(ChangePasswordDto dto, CancellationToken ct = default)
    {
        await passwordValidator.ValidateAndThrowAsync(dto, ct);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == pengguna.Id, ct)
            ?? throw new UnauthorizedException("Akun tidak ditemukan. Silakan login ulang.");
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, dto.PasswordLama) == PasswordVerificationResult.Failed)
            throw new ValidationException([new ValidationFailure("PasswordLama", "Kata sandi lama salah.")]);

        user.PasswordHash = hasher.HashPassword(user, dto.PasswordBaru);
        await SesiLogin.CabutSemuaAsync(db, user.Id, ct);
        return await BuatSesiAsync(user, digantikan: null, ct);
    }

    private async Task<LoginResponseDto> BuatSesiAsync(User user, RefreshToken? digantikan, CancellationToken ct)
    {
        var refresh = SesiLogin.TokenAcak();
        var baru = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = SesiLogin.Hash(refresh),
            ExpiresAt = DateTime.UtcNow.Add(pembuatToken.MasaRefreshToken),
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.RefreshTokens.Add(baru);
        if (digantikan is not null)
        {
            digantikan.RevokedAt = DateTime.UtcNow;
            digantikan.ReplacedById = baru.Id;
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var akses = pembuatToken.BuatAccessToken(user);
        var crewId = await db.Crews.AsNoTracking().Where(c => c.UserId == user.Id).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);
        return new LoginResponseDto(
            akses.Token, akses.KedaluwarsaUtc, refresh, baru.ExpiresAt,
            new AuthUserDto(user.Id, user.Nama, user.Email, user.Role, crewId));
    }
}

/// <summary>Utilitas refresh token yang juga dipakai UserService (reset kata sandi, nonaktifkan akun).</summary>
internal static class SesiLogin
{
    /// <summary>48 byte acak, base64url (64 karakter).</summary>
    public static string TokenAcak() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>SHA-256 heksadesimal (64 karakter). Hanya ini yang disimpan di database.</summary>
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    /// <summary>Tandai semua refresh token aktif milik akun sebagai dicabut. Perlu SaveChanges setelahnya.</summary>
    public static async Task CabutSemuaAsync(IAppDbContext db, Guid userId, CancellationToken ct)
    {
        var aktif = await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct);
        var sekarang = DateTime.UtcNow;
        foreach (var t in aktif) t.RevokedAt = sekarang;
    }
}
