using NsCreative.Core.Enums;

namespace NsCreative.Core.Common;

/// <summary>
/// Pengguna yang sedang login, dibaca dari klaim access token (bukan dari parameter request).
/// Diimplementasikan di project Api. Hanya boleh dipakai di endpoint yang butuh login.
/// </summary>
public interface IPenggunaSaatIni
{
    Guid Id { get; }
    UserRole Role { get; }
}
