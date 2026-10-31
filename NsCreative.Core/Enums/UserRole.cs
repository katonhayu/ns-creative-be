using System.Text.Json;

namespace NsCreative.Core.Enums;

/// <summary>
/// Disimpan sebagai snake_case: admin, event_manager, crew.
/// Hak kelola sebuah event tidak ditentukan peran, tetapi oleh siapa PIC event tersebut
/// (crew yang ditautkan ke akun). Lihat <c>AksesEvent</c>.
/// </summary>
public enum UserRole
{
    /// <summary>Kelola semua data.</summary>
    Admin,

    /// <summary>Melihat semua menu dan semua event, tidak pernah mengubah apa pun.</summary>
    EventManager,

    /// <summary>Kelola event yang PIC-nya dirinya; event lain tempat ia terlibat hanya bisa dilihat.</summary>
    Crew,
}

public static class UserRoleExtensions
{
    /// <summary>Nilai klaim "role" di JWT, sama dengan nilai di database: admin, event_manager, crew.</summary>
    public static string NamaKlaim(this UserRole role) => JsonNamingPolicy.SnakeCaseLower.ConvertName(role.ToString());

    public static UserRole? DariKlaim(string? klaim) =>
        Enum.GetValues<UserRole>().Select(r => (UserRole?)r).FirstOrDefault(r => r!.Value.NamaKlaim() == klaim);
}
