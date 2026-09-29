namespace NsCreative.Core.Entities;

public class Client : BaseEntity, ISoftDelete
{
    public string Nama { get; set; } = "";
    public string? Perusahaan { get; set; }
    public string? Telepon { get; set; }
    public string? Email { get; set; }
    public string? Alamat { get; set; }
    public DateTime? DeletedAt { get; set; }
}
