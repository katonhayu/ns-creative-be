using System.Text.RegularExpressions;
using FluentValidation;

namespace NsCreative.Core.Validators;

/// <summary>Aturan validasi yang berulang di banyak DTO. Semua pesan berbahasa Indonesia.</summary>
public static partial class AturanValidasi
{
    [GeneratedRegex(@"^(\+62|0)[0-9\s-]{7,16}$")]
    private static partial Regex PolaTelepon();

    /// <summary>Teks wajib diisi dengan panjang maksimal.</summary>
    public static IRuleBuilderOptions<T, string> Wajib<T>(this IRuleBuilder<T, string> rule, string label, int maks) =>
        rule
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage($"{label} harus diisi.")
            .MaximumLength(maks).WithMessage($"{label} maksimal {maks} karakter.");

    /// <summary>Teks opsional dengan panjang maksimal.</summary>
    public static IRuleBuilderOptions<T, string?> Maksimal<T>(this IRuleBuilder<T, string?> rule, string label, int maks) =>
        rule.MaximumLength(maks).WithMessage($"{label} maksimal {maks} karakter.");

    /// <summary>Nomor telepon Indonesia opsional (kolom varchar(20)).</summary>
    public static IRuleBuilderOptions<T, string?> TeleponOpsional<T>(this IRuleBuilder<T, string?> rule) =>
        rule
            .MaximumLength(20).WithMessage("Telepon maksimal 20 karakter.")
            .Must(v => string.IsNullOrWhiteSpace(v) || PolaTelepon().IsMatch(v.Trim()))
            .WithMessage("Nomor telepon tidak valid. Contoh: 0812-3456-7890.");

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex PolaEmail();

    /// <summary>Email opsional (kolom varchar(100)). Kosong atau null dianggap tidak diisi.</summary>
    public static IRuleBuilderOptions<T, string?> EmailOpsional<T>(this IRuleBuilder<T, string?> rule) =>
        rule
            .MaximumLength(100).WithMessage("Email maksimal 100 karakter.")
            .Must(v => string.IsNullOrWhiteSpace(v) || PolaEmail().IsMatch(v.Trim()))
            .WithMessage("Format email tidak valid.");

    /// <summary>Email wajib (kolom varchar(100)).</summary>
    public static IRuleBuilderOptions<T, string> EmailWajib<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Email harus diisi.")
            .MaximumLength(100).WithMessage("Email maksimal 100 karakter.")
            .Must(v => string.IsNullOrWhiteSpace(v) || PolaEmail().IsMatch(v.Trim()))
            .WithMessage("Format email tidak valid.");

    /// <summary>Nominal uang wajib, tidak boleh negatif.</summary>
    public static IRuleBuilderOptions<T, decimal> Nominal<T>(this IRuleBuilder<T, decimal> rule, string label) =>
        rule.GreaterThanOrEqualTo(0).WithMessage($"{label} tidak boleh negatif.");
}
