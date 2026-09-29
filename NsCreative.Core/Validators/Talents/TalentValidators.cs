using NsCreative.Core.Dtos.Talents;
using FluentValidation;

namespace NsCreative.Core.Validators.Talents;

/// <summary>Aturan bersama create dan update talent, sesuai panjang kolom tabel talents.</summary>
public abstract class TalentInputValidator<T> : AbstractValidator<T> where T : ITalentInput
{
    protected TalentInputValidator()
    {
        RuleFor(x => x.Nama).Wajib("Nama talent", 100);
        RuleFor(x => x.Jenis).Wajib("Jenis talent", 50);
        RuleFor(x => x.ContactPerson).Maksimal("Contact person", 100);
        RuleFor(x => x.Telepon).TeleponOpsional();
        RuleFor(x => x.FeeStandar).Nominal("Fee standar");
        RuleFor(x => x.RiderDefault).Maksimal("Rider default", 1000);
    }
}

public class CreateTalentDtoValidator : TalentInputValidator<CreateTalentDto>;

public class UpdateTalentDtoValidator : TalentInputValidator<UpdateTalentDto>;
