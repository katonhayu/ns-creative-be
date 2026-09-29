using UUIDNext;

namespace NsCreative.Core.Common;

/// <summary>
/// Pembuat Id entity. UUIDv7 berurut waktu sehingga ramah index B-tree.
/// Saat naik ke .NET 9, cukup ganti isinya dengan <c>Guid.CreateVersion7()</c>.
/// </summary>
public static class NewId
{
    public static Guid Create() => Uuid.NewDatabaseFriendly(Database.PostgreSql);
}
