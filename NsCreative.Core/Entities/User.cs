using NsCreative.Core.Enums;

namespace NsCreative.Core.Entities;

public class User : BaseEntity
{
    public string Nama { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; }
    public bool IsAktif { get; set; } = true;
}
