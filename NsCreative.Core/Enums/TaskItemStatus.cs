namespace NsCreative.Core.Enums;

/// <summary>
/// Status task. Dinamai TaskItemStatus, bukan TaskStatus, agar tidak bentrok dengan
/// System.Threading.Tasks.TaskStatus yang ikut ter-import lewat ImplicitUsings.
/// Disimpan sebagai snake_case: belum, dikerjakan, selesai, terlambat.
/// </summary>
public enum TaskItemStatus
{
    Belum,
    Dikerjakan,
    Selesai,
    Terlambat,
}
