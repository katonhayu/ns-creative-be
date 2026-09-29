using System.Text;

namespace NsCreative.Api.Auth;

/// <summary>Bagian "Jwt" di appsettings.json. Untuk production, isi Key lewat environment variable Jwt__Key.</summary>
public class JwtOptions
{
    public const string Bagian = "Jwt";
    private const int PanjangKunciMinimal = 32;

    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string Key { get; set; } = "";
    public int AccessTokenMenit { get; set; } = 15;
    public int RefreshTokenHari { get; set; } = 7;

    /// <summary>Aplikasi menolak jalan jika konfigurasi tidak aman, daripada diam-diam memakai kunci lemah.</summary>
    public void Validasi()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
            throw new InvalidOperationException("Jwt:Issuer dan Jwt:Audience harus diisi di appsettings.json.");
        if (Encoding.UTF8.GetByteCount(Key) < PanjangKunciMinimal)
            throw new InvalidOperationException(
                $"Jwt:Key minimal {PanjangKunciMinimal} karakter. Isi di appsettings.json atau environment variable Jwt__Key.");
        if (AccessTokenMenit <= 0 || RefreshTokenHari <= 0)
            throw new InvalidOperationException("Jwt:AccessTokenMenit dan Jwt:RefreshTokenHari harus lebih dari 0.");
    }
}
