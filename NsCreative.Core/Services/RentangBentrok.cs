using FluentValidation;
using FluentValidation.Results;

namespace NsCreative.Core.Services;

/// <summary>Validasi query string endpoint <c>/{crews|talents|vendors}/{id}/conflicts</c>.</summary>
internal static class RentangBentrok
{
    /// <summary>tanggalMulai wajib; tanggalSelesai opsional (default sama dengan tanggalMulai).</summary>
    public static (DateOnly Dari, DateOnly Sampai) Validasi(DateOnly? tanggalMulai, DateOnly? tanggalSelesai)
    {
        if (tanggalMulai is not { } dari)
            throw new ValidationException([new ValidationFailure("TanggalMulai", "Tanggal mulai harus diisi.")]);
        var sampai = tanggalSelesai ?? dari;
        if (sampai < dari)
            throw new ValidationException(
                [new ValidationFailure("TanggalSelesai", "Tanggal selesai tidak boleh lebih awal dari tanggal mulai.")]);
        return (dari, sampai);
    }
}
