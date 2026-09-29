using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NsCreative.Api.Json;

/// <summary>
/// Jam diterima sebagai "HH:mm" (dari input time di browser) atau "HH:mm:ss", dan selalu
/// dikirim balik sebagai "HH:mm:ss" sesuai kontrak. Converter bawaan hanya menerima format kedua.
/// </summary>
public class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    private static readonly string[] Format = ["HH:mm:ss", "HH:mm", "HH:mm:ss.FFFFFFF"];

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String
            && TimeOnly.TryParseExact(reader.GetString(), Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var waktu))
            return waktu;
        throw new JsonException("Format jam harus HH:mm atau HH:mm:ss.");
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
}
