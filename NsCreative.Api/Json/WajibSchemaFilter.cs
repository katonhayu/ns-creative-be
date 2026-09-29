using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace NsCreative.Api.Json;

/// <summary>
/// Semua property ditandai <c>required</c> di OpenAPI, karena System.Text.Json selalu menulis
/// setiap property (yang kosong sebagai <c>null</c>). Nullability tetap dari
/// <c>SupportNonNullableReferenceTypes()</c>. Hasilnya generator tipe frontend (openapi-typescript)
/// menghasilkan <c>perusahaan: string | null</c>, bukan <c>perusahaan?: string | null</c>.
/// </summary>
public class WajibSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema.Properties is null) return;
        foreach (var nama in schema.Properties.Keys) schema.Required.Add(nama);
    }
}
