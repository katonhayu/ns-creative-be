using NsCreative.Core.Enums;

namespace NsCreative.Core.Entities;

public class EventVendor : BaseEntity
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid VendorId { get; set; }
    public Vendor Vendor { get; set; } = null!;

    public string? Kebutuhan { get; set; }
    public decimal Biaya { get; set; }
    public StatusPembayaran StatusPembayaran { get; set; } = StatusPembayaran.BelumBayar;
}
