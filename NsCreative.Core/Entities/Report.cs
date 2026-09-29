namespace NsCreative.Core.Entities;

/// <summary>Laporan evaluasi, satu per event.</summary>
public class Report : BaseEntity
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public string? HasilPelaksanaan { get; set; }
    /// <summary>Snapshot SUM(budgets.biaya_aktual) saat laporan dibuat. Nilainya dibekukan.</summary>
    public decimal TotalPengeluaran { get; set; }
    public string? Kendala { get; set; }
    public string? Evaluasi { get; set; }
    public string? Catatan { get; set; }
    public DateOnly TanggalDibuat { get; set; }
}
