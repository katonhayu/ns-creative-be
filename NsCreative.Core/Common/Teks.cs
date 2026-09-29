namespace NsCreative.Core.Common;

public static class Teks
{
    /// <summary>Teks opsional: dipangkas, dan string kosong disimpan sebagai null.</summary>
    public static string? Bersihkan(string? nilai) => string.IsNullOrWhiteSpace(nilai) ? null : nilai.Trim();

    /// <summary>Teks wajib: dipangkas. Validator sudah memastikan isinya tidak kosong.</summary>
    public static string Wajib(string nilai) => nilai.Trim();

    /// <summary>Email disimpan huruf kecil supaya pencarian dan keunikan konsisten.</summary>
    public static string? Email(string? nilai) => Bersihkan(nilai)?.ToLowerInvariant();
}
