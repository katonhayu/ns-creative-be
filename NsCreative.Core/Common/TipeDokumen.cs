namespace NsCreative.Core.Common;

/// <summary>
/// Tipe file dokumen yang diizinkan. Content-Type ditentukan server dari ekstensi, dan isi
/// file dicocokkan dengan tanda tangan (magic bytes) formatnya, supaya file .exe yang diganti
/// namanya jadi .pdf tetap ditolak.
/// </summary>
public static class TipeDokumen
{
    public const long UkuranMaks = 10 * 1024 * 1024;

    public static readonly IReadOnlyDictionary<string, string> ContentType =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".doc"] = "application/msword",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xls"] = "application/vnd.ms-excel",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
        };

    public static string DaftarEkstensi => string.Join(", ", ContentType.Keys.Select(k => k.TrimStart('.')));

    public static bool Diizinkan(string? ekstensi) => ekstensi is not null && ContentType.ContainsKey(ekstensi);

    private static readonly byte[] Pdf = "%PDF"u8.ToArray();
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04];                          // docx, xlsx
    private static readonly byte[] Ole = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];  // doc, xls

    /// <summary>Apakah byte awal file cocok dengan format ekstensinya.</summary>
    public static bool IsiSesuai(string ekstensi, ReadOnlySpan<byte> awal) => ekstensi.ToLowerInvariant() switch
    {
        ".pdf" => awal.StartsWith(Pdf),
        ".png" => awal.StartsWith(Png),
        ".jpg" or ".jpeg" => awal.StartsWith(Jpeg),
        ".docx" or ".xlsx" => awal.StartsWith(Zip),
        ".doc" or ".xls" => awal.StartsWith(Ole),
        _ => false,
    };

    /// <summary>Nama file unduhan dari nama dokumen + ekstensi file tersimpan, tanpa karakter terlarang.</summary>
    public static string NamaUnduhan(string namaDokumen, string filePath)
    {
        var terlarang = Path.GetInvalidFileNameChars();
        var nama = new string(namaDokumen.Select(c => terlarang.Contains(c) ? '_' : c).ToArray()).Trim();
        return (nama.Length == 0 ? "dokumen" : nama) + Path.GetExtension(filePath);
    }
}
