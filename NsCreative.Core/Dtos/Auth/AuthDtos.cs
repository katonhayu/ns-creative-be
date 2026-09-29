using NsCreative.Core.Enums;

namespace NsCreative.Core.Dtos.Auth;

public record LoginDto(string Email, string Password);

public record RefreshTokenDto(string RefreshToken);

public record ChangePasswordDto(string PasswordLama, string PasswordBaru);

/// <summary>Profil pengguna login.</summary>
/// <param name="CrewId">Data crew yang ditautkan ke akun ini (hanya untuk peran crew).</param>
public record AuthUserDto(Guid Id, string Nama, string Email, UserRole Role, Guid? CrewId);

/// <summary>
/// Hasil login, refresh, dan ganti kata sandi. Access token dikirim di header
/// <c>Authorization: Bearer ...</c>; refresh token hanya dikirim ke <c>/api/auth/refresh</c> dan <c>/logout</c>.
/// </summary>
public record LoginResponseDto(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    AuthUserDto User);
