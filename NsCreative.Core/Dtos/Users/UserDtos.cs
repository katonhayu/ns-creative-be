using NsCreative.Core.Enums;

namespace NsCreative.Core.Dtos.Users;

/// <summary>Detail pengguna. Hash password tidak pernah dikirim.</summary>
public record UserDto(
    Guid Id,
    string Nama,
    string Email,
    UserRole Role,
    bool IsAktif,
    int JumlahEventPic,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record UserListDto(Guid Id, string Nama, string Email, UserRole Role, bool IsAktif, int JumlahEventPic);

/// <summary>Pilihan dropdown, misalnya PIC event. Hanya pengguna aktif.</summary>
public record UserLookupDto(Guid Id, string Nama, string Email, UserRole Role);

public record CreateUserDto(string Nama, string Email, string Password, UserRole Role);

/// <summary>Ubah nama, email, dan peran. Status dan kata sandi punya endpoint sendiri.</summary>
public record UpdateUserDto(string Nama, string Email, UserRole Role);

public record UpdateUserStatusDto(bool IsAktif);

public record ResetPasswordDto(string PasswordBaru);
