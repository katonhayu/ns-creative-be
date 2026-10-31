using NsCreative.Core.Dtos.Users;
using FluentValidation;

namespace NsCreative.Core.Validators.Users;

internal static class AturanPassword
{
    public const int Minimal = 8;
    public const int Maksimal = 100;

    public static IRuleBuilderOptions<T, string> Password<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .Must(v => !string.IsNullOrEmpty(v)).WithMessage("Kata sandi harus diisi.")
            .MinimumLength(Minimal).WithMessage($"Kata sandi minimal {Minimal} karakter.")
            .MaximumLength(Maksimal).WithMessage($"Kata sandi maksimal {Maksimal} karakter.");
}

public class CreateUserDtoValidator : AbstractValidator<CreateUserDto>
{
    public CreateUserDtoValidator()
    {
        RuleFor(x => x.Nama).Wajib("Nama", 100);
        RuleFor(x => x.Email).EmailWajib();
        RuleFor(x => x.Password).Password();
        RuleFor(x => x.Role).IsInEnum().WithMessage("Peran harus admin, event_manager, atau crew.");
    }
}

public class UpdateUserDtoValidator : AbstractValidator<UpdateUserDto>
{
    public UpdateUserDtoValidator()
    {
        RuleFor(x => x.Nama).Wajib("Nama", 100);
        RuleFor(x => x.Email).EmailWajib();
        RuleFor(x => x.Role).IsInEnum().WithMessage("Peran harus admin, event_manager, atau crew.");
    }
}

public class ResetPasswordDtoValidator : AbstractValidator<ResetPasswordDto>
{
    public ResetPasswordDtoValidator()
    {
        RuleFor(x => x.PasswordBaru).Password();
    }
}
