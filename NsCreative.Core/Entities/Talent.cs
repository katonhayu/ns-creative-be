namespace NsCreative.Core.Entities;

public class Talent : BaseEntity, ISoftDelete
{
    public string Nama { get; set; } = "";
    public string Jenis { get; set; } = "";
    public string? ContactPerson { get; set; }
    public string? Telepon { get; set; }
    public decimal FeeStandar { get; set; }
    public string? RiderDefault { get; set; }
    public DateTime? DeletedAt { get; set; }
}
