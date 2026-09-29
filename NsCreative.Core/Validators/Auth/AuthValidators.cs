using NsCreative.Core.Dtos.Auth;
using NsCreative.Core.Validators.Users;
using FluentValidation;

namespace NsCreative.Core.Validators.Auth;

public class LoginDtoValidator : AbstractValidator<LoginDto>
{
    public LoginDtoValidator()
    {
        RuleFor(x => x.Email).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Email harus diisi.");
        RuleFor(x => x.Password).Must(v => !string.IsNullOrEmpty(v)).WithMessage("Kata sandi harus diisi.");
    }
}

public class RefreshTokenDtoValidator : AbstractValidator<RefreshTokenDto>
{
    public RefreshTokenDtoValidator()
    {
        RuleFor(x => x.RefreshToken).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Refresh token harus diisi.");
    }
}

public class ChangePasswordDtoValidator : AbstractValidator<ChangePasswordDto>
{
    public ChangePasswordDtoValidator()
    {
        RuleFor(x => x.PasswordLama).Must(v => !string.IsNullOrEmpty(v)).WithMessage("Kata sandi lama harus diisi.");
        RuleFor(x => x.PasswordBaru)
            .Password()
            .NotEqual(x => x.PasswordLama).WithMessage("Kata sandi baru harus berbeda dari yang lama.");
    }
}
