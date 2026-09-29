using NsCreative.Core.Common;
using NsCreative.Core.Enums;
using NsCreative.Core.Exceptions;

namespace NsCreative.Api.Auth;

/// <summary>Pengguna login dari klaim access token yang sudah divalidasi JwtBearer.</summary>
public class PenggunaSaatIni(IHttpContextAccessor http) : IPenggunaSaatIni
{
    public Guid Id =>
        Guid.TryParse(Klaim(KlaimJwt.Sub), out var id)
            ? id
            : throw new UnauthorizedException("Silakan login terlebih dahulu.");

    public UserRole Role =>
        UserRoleExtensions.DariKlaim(Klaim(KlaimJwt.Role))
        ?? throw new UnauthorizedException("Silakan login terlebih dahulu.");

    private string? Klaim(string jenis) => http.HttpContext?.User.FindFirst(jenis)?.Value;
}
