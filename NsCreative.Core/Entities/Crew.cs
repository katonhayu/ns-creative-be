namespace NsCreative.Core.Entities;

public class Crew : BaseEntity, ISoftDelete
{
    /// <summary>Akun login crew, jika crew ini perlu melihat rundown dan task miliknya.</summary>
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public string Nama { get; set; } = "";
    public string? Telepon { get; set; }
    public string? Keahlian { get; set; }
    public DateTime? DeletedAt { get; set; }
}
