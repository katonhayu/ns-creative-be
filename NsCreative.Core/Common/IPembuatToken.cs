using NsCreative.Core.Entities;

namespace NsCreative.Core.Common;

/// <summary>
/// Pembuat access token (JWT). Diimplementasikan di project Api karena pustaka JWT hanya
/// dipasang di sana; logika sesi dan refresh token tetap di AuthService.
/// </summary>
public interface IPembuatToken
{
    TokenAkses BuatAccessToken(User user);

    /// <summary>Masa berlaku refresh token (konfigurasi Jwt:RefreshTokenHari).</summary>
    TimeSpan MasaRefreshToken { get; }
}

public record TokenAkses(string Token, DateTime KedaluwarsaUtc);
