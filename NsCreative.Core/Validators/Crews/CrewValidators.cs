using NsCreative.Core.Dtos.Crews;
using FluentValidation;

namespace NsCreative.Core.Validators.Crews;

/// <summary>
/// Aturan bersama create dan update crew. Keabsahan akun (ada, berperan crew, belum
/// ditautkan) dicek di service karena butuh database.
/// </summary>
public abstract class CrewInputValidator<T> : AbstractValidator<T> where T : ICrewInput
{
    protected CrewInputValidator()
    {
        RuleFor(x => x.Nama).Wajib("Nama crew", 100);
        RuleFor(x => x.Telepon).TeleponOpsional();
        RuleFor(x => x.Keahlian).Maksimal("Keahlian", 200);
        RuleFor(x => x.UserId)
            .Must(id => id != Guid.Empty).WithMessage("Akun login tidak valid.")
            .When(x => x.UserId.HasValue);
    }
}

public class CreateCrewDtoValidator : CrewInputValidator<CreateCrewDto>;

public class UpdateCrewDtoValidator : CrewInputValidator<UpdateCrewDto>;
