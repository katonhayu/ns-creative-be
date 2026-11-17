namespace NsCreative.Core.Common;

/// <summary>
/// Waktu bisnis mengikuti WIB (Asia/Jakarta), sedangkan kolom timestamptz disimpan dalam UTC.
/// </summary>
public static class Waktu
{
    private static readonly TimeZoneInfo Wib = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta");

    /// <summary>Tanggal hari ini menurut WIB, dipakai untuk hitung mundur hari-H dan penanda terlambat.</summary>
    public static DateOnly HariIni() => DateOnly.FromDateTime(Sekarang());

    /// <summary>Jam sekarang menurut WIB, misalnya untuk waktu cetak laporan.</summary>
    public static DateTime Sekarang() => KeWib(DateTime.UtcNow);

    /// <summary>Ubah waktu UTC dari kolom timestamptz ke WIB untuk ditampilkan.</summary>
    public static DateTime KeWib(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Wib);

    /// <summary>
    /// Ubah ke UTC sebelum disimpan ke kolom timestamptz. Waktu tanpa zona, misalnya
    /// "2026-12-20T20:00:00" dari form, dianggap WIB.
    /// </summary>
    public static DateTime? KeUtc(DateTime? waktu) => waktu switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } utc => utc,
        { Kind: DateTimeKind.Local } lokal => lokal.ToUniversalTime(),
        { } tanpaZona => TimeZoneInfo.ConvertTimeToUtc(tanpaZona, Wib),
    };
}
