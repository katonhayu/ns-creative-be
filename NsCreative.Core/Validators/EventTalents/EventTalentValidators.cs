using NsCreative.Core.Dtos.EventTalents;
using FluentValidation;

namespace NsCreative.Core.Validators.EventTalents;

public abstract class EventTalentInputValidator<T> : AbstractValidator<T> where T : IEventTalentInput
{
    protected EventTalentInputValidator()
    {
        RuleFor(x => x.FeeDeal).Nominal("Fee deal");
        RuleFor(x => x.DurasiMenit)
            .InclusiveBetween(1, 1440).WithMessage("Durasi tampil harus antara 1 dan 1440 menit.")
            .When(x => x.DurasiMenit.HasValue);
    }
}

public class CreateEventTalentDtoValidator : EventTalentInputValidator<CreateEventTalentDto>
{
    public CreateEventTalentDtoValidator()
    {
        RuleFor(x => x.TalentId).NotEqual(Guid.Empty).WithMessage("Talent harus dipilih.");
        RuleFor(x => x.Status).IsInEnum().WithMessage("Status talent tidak dikenal.").When(x => x.Status.HasValue);
    }
}

public class UpdateEventTalentDtoValidator : EventTalentInputValidator<UpdateEventTalentDto>;

public class UpdateEventTalentStatusDtoValidator : AbstractValidator<UpdateEventTalentStatusDto>
{
    public UpdateEventTalentStatusDtoValidator()
    {
        RuleFor(x => x.Status)
            .NotNull().WithMessage("Status harus diisi.")
            .IsInEnum().WithMessage("Status talent tidak dikenal.");
    }
}
