using System.Security.Claims;
using System.Text;
using NsCreative.Core.Common;
using NsCreative.Core.Entities;
using NsCreative.Core.Enums;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace NsCreative.Api.Auth;

/// <summary>Access token JWT (HMAC-SHA256) berisi sub, email, name, dan role.</summary>
public class PembuatTokenJwt(JwtOptions opsi) : IPembuatToken
{
    private readonly SigningCredentials _kredensial = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opsi.Key)), SecurityAlgorithms.HmacSha256);

    public TimeSpan MasaRefreshToken => TimeSpan.FromDays(opsi.RefreshTokenHari);

    public TokenAkses BuatAccessToken(User user)
    {
        var kedaluwarsa = DateTime.UtcNow.AddMinutes(opsi.AccessTokenMenit);
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = opsi.Issuer,
            Audience = opsi.Audience,
            Expires = kedaluwarsa,
            SigningCredentials = _kredensial,
            Subject = new ClaimsIdentity(
            [
                new Claim(KlaimJwt.Sub, user.Id.ToString()),
                new Claim(KlaimJwt.Email, user.Email),
                new Claim(KlaimJwt.Nama, user.Nama),
                new Claim(KlaimJwt.Role, user.Role.NamaKlaim()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ]),
        });
        return new TokenAkses(token, kedaluwarsa);
    }
}

/// <summary>Nama klaim apa adanya (MapInboundClaims = false), jadi sama di token dan di HttpContext.User.</summary>
public static class KlaimJwt
{
    public const string Sub = JwtRegisteredClaimNames.Sub;
    public const string Email = JwtRegisteredClaimNames.Email;
    public const string Nama = "name";
    public const string Role = "role";
}
