namespace NsCreative.Core.Entities;

public class Vendor : BaseEntity, ISoftDelete
{
    public string Nama { get; set; } = "";
    public string JenisJasa { get; set; } = "";
    public string? ContactPerson { get; set; }
    public string? Telepon { get; set; }
    public DateTime? DeletedAt { get; set; }
}
