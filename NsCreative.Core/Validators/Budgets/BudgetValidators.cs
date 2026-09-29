using NsCreative.Core.Dtos.Budgets;
using FluentValidation;

namespace NsCreative.Core.Validators.Budgets;

public abstract class BudgetInputValidator<T> : AbstractValidator<T> where T : IBudgetInput
{
    protected BudgetInputValidator()
    {
        RuleFor(x => x.NamaKebutuhan).Wajib("Nama kebutuhan", 150);
        RuleFor(x => x.Kategori).Wajib("Kategori", 50);
        RuleFor(x => x.EstimasiBiaya).Nominal("Estimasi biaya");
        RuleFor(x => x.BiayaAktual).Nominal("Biaya aktual");
        RuleFor(x => x.Sponsorship).Nominal("Sponsorship");
        RuleFor(x => x.StatusPembayaran)
            .IsInEnum().WithMessage("Status pembayaran tidak dikenal.")
            .When(x => x.StatusPembayaran.HasValue);
    }
}

public class CreateBudgetDtoValidator : BudgetInputValidator<CreateBudgetDto>;

public class UpdateBudgetDtoValidator : BudgetInputValidator<UpdateBudgetDto>;

public class UpdateRealisasiDtoValidator : AbstractValidator<UpdateRealisasiDto>
{
    public UpdateRealisasiDtoValidator()
    {
        RuleFor(x => x.BiayaAktual)
            .NotNull().WithMessage("Biaya aktual harus diisi.")
            .GreaterThanOrEqualTo(0).WithMessage("Biaya aktual tidak boleh negatif.");
        RuleFor(x => x.StatusPembayaran)
            .NotNull().WithMessage("Status pembayaran harus diisi.")
            .IsInEnum().WithMessage("Status pembayaran tidak dikenal.");
        RuleFor(x => x.Sponsorship)
            .GreaterThanOrEqualTo(0).WithMessage("Sponsorship tidak boleh negatif.")
            .When(x => x.Sponsorship.HasValue);
    }
}
