namespace NsCreative.Core.Enums;

/// <summary>Disimpan sebagai snake_case: admin, pic, crew.</summary>
public enum UserRole
{
    Admin,
    Pic,
    Crew,
}

public static class UserRoleExtensions
{
    /// <summary>Nilai klaim "role" di JWT, sama dengan nilai di database: admin, pic, crew.</summary>
    public static string NamaKlaim(this UserRole role) => role.ToString().ToLowerInvariant();

    public static UserRole? DariKlaim(string? klaim) =>
        Enum.GetValues<UserRole>().Select(r => (UserRole?)r).FirstOrDefault(r => r!.Value.NamaKlaim() == klaim);
}
