using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace NsCreative.Data;

/// <summary>
/// Menyimpan enum sebagai string snake_case, misalnya StatusPembayaran.BelumBayar
/// menjadi "belum_bayar". Aturan penamaannya sama dengan JsonNamingPolicy.SnakeCaseLower,
/// jadi nilai di database dan di JSON API selalu identik.
/// </summary>
public class SnakeCaseEnumConverter<TEnum>()
    : ValueConverter<TEnum, string>(v => KeString(v), v => DariString(v))
    where TEnum : struct, Enum
{
    private static readonly Dictionary<TEnum, string> KeDb = Enum.GetValues<TEnum>()
        .ToDictionary(v => v, v => JsonNamingPolicy.SnakeCaseLower.ConvertName(v.ToString()));

    private static readonly Dictionary<string, TEnum> DariDb = KeDb.ToDictionary(p => p.Value, p => p.Key);

    private static string KeString(TEnum nilai) => KeDb[nilai];

    private static TEnum DariString(string nilai) =>
        DariDb.TryGetValue(nilai, out var hasil)
            ? hasil
            : throw new InvalidOperationException($"Nilai '{nilai}' tidak dikenal untuk enum {typeof(TEnum).Name}.");
}
