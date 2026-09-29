using NsCreative.Core.Common;

namespace NsCreative.Core.Entities;

/// <summary>
/// Kolom yang dimiliki semua tabel. Id dibuat di C# (bukan oleh database) supaya
/// sudah diketahui sebelum SaveChanges. CreatedAt dan UpdatedAt diisi database:
/// DEFAULT now() saat insert, dan trigger set_updated_at() saat update.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = NewId.Create();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Master data yang dihapus secara lunak (soft delete) lewat DeletedAt.</summary>
public interface ISoftDelete
{
    DateTime? DeletedAt { get; set; }
}
