using NsCreative.Core.Dtos.EventCrews;
using FluentValidation;

namespace NsCreative.Core.Validators.EventCrews;

public abstract class EventCrewInputValidator<T> : AbstractValidator<T> where T : IEventCrewInput
{
    protected EventCrewInputValidator()
    {
        RuleFor(x => x.Posisi).Wajib("Posisi", 100);
    }
}

public class CreateEventCrewDtoValidator : EventCrewInputValidator<CreateEventCrewDto>
{
    public CreateEventCrewDtoValidator()
    {
        RuleFor(x => x.CrewId).NotEqual(Guid.Empty).WithMessage("Crew harus dipilih.");
    }
}

public class UpdateEventCrewDtoValidator : EventCrewInputValidator<UpdateEventCrewDto>;
