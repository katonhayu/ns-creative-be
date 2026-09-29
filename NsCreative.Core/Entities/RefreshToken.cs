namespace NsCreative.Core.Entities;

/// <summary>
/// Refresh token login. Yang disimpan hanya hash SHA-256-nya, jadi isi tabel ini tidak bisa
/// dipakai untuk login walau bocor. Setiap refresh memutar token: yang lama dicabut dan
/// menunjuk ke penggantinya lewat ReplacedById.
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? ReplacedById { get; set; }
}
