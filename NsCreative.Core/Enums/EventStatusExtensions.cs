namespace NsCreative.Core.Enums;

public static class EventStatusExtensions
{
    /// <summary>
    /// Event yang masih berjalan atau direncanakan. Master data yang dipakai event
    /// dengan status ini tidak boleh dihapus.
    /// </summary>
    public static readonly EventStatus[] Aktif =
        [EventStatus.Draft, EventStatus.Perencanaan, EventStatus.Dikonfirmasi, EventStatus.Berlangsung];

    /// <summary>Transisi status yang sah (CLAUDE.md bagian 10). Status akhir tidak punya lanjutan.</summary>
    private static readonly Dictionary<EventStatus, EventStatus[]> Transisi = new()
    {
        [EventStatus.Draft] = [EventStatus.Perencanaan, EventStatus.Dibatalkan],
        [EventStatus.Perencanaan] = [EventStatus.Dikonfirmasi, EventStatus.Dibatalkan],
        [EventStatus.Dikonfirmasi] = [EventStatus.Berlangsung, EventStatus.Dibatalkan],
        [EventStatus.Berlangsung] = [EventStatus.Selesai],
        [EventStatus.Selesai] = [],
        [EventStatus.Dibatalkan] = [],
    };

    public static IReadOnlyList<EventStatus> StatusBerikutnya(this EventStatus status) => Transisi[status];

    public static bool BolehMenjadi(this EventStatus dari, EventStatus ke) => Transisi[dari].Contains(ke);

    /// <summary>
    /// Event selesai atau dibatalkan: talent, crew, vendor, jadwal, task, dan budget-nya
    /// tidak boleh diubah lagi. Yang masih boleh hanya laporan.
    /// </summary>
    public static bool Terkunci(this EventStatus status) =>
        status is EventStatus.Selesai or EventStatus.Dibatalkan;

    /// <summary>Hanya event yang belum berjalan atau sudah dibatalkan yang boleh dihapus permanen.</summary>
    public static bool BolehDihapus(this EventStatus status) =>
        status is EventStatus.Draft or EventStatus.Dibatalkan;

    /// <summary>Nama status untuk pesan error, sama dengan nilai di database (misalnya "dikonfirmasi").</summary>
    public static string Label(this EventStatus status) => status.ToString().ToLowerInvariant();
}
