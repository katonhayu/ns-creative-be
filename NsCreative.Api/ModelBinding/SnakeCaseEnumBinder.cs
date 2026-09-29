using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace NsCreative.Api.ModelBinding;

/// <summary>
/// Enum di query string dan route memakai nilai snake_case yang sama dengan JSON
/// (<c>?statusPembayaran=belum_bayar</c>). Binder bawaan hanya mengenali nama C# (BelumBayar).
/// Body JSON tidak terpengaruh; itu ditangani JsonStringEnumConverter.
/// </summary>
public class SnakeCaseEnumBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (context.BindingInfo.BindingSource == BindingSource.Body) return null;
        var tipe = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
        return tipe.IsEnum ? new SnakeCaseEnumBinder(tipe) : null;
    }
}

public class SnakeCaseEnumBinder(Type tipe) : IModelBinder
{
    private readonly Dictionary<string, object> _nilai = Enum.GetValues(tipe)
        .Cast<object>()
        .ToDictionary(v => JsonNamingPolicy.SnakeCaseLower.ConvertName(v.ToString()!), StringComparer.OrdinalIgnoreCase);

    public Task BindModelAsync(ModelBindingContext context)
    {
        var hasil = context.ValueProvider.GetValue(context.ModelName);
        if (hasil == ValueProviderResult.None) return Task.CompletedTask;
        context.ModelState.SetModelValue(context.ModelName, hasil);

        var teks = hasil.FirstValue?.Trim();
        if (string.IsNullOrEmpty(teks)) return Task.CompletedTask;

        if (_nilai.TryGetValue(teks, out var nilai))
            context.Result = ModelBindingResult.Success(nilai);
        else
            context.ModelState.TryAddModelError(context.ModelName,
                $"Nilai '{teks}' tidak dikenal. Pilihan: {string.Join(", ", _nilai.Keys)}.");
        return Task.CompletedTask;
    }
}
