using NsCreative.Core.Dtos.Reports;
using FluentValidation;

namespace NsCreative.Core.Validators.Reports;

/// <summary>Kolomnya text; batas panjang hanya pengaman dari kiriman yang tidak wajar.</summary>
public abstract class ReportInputValidator<T> : AbstractValidator<T> where T : IReportInput
{
    private const int Maks = 10_000;

    protected ReportInputValidator()
    {
        RuleFor(x => x.HasilPelaksanaan).Maksimal("Hasil pelaksanaan", Maks);
        RuleFor(x => x.Kendala).Maksimal("Kendala", Maks);
        RuleFor(x => x.Evaluasi).Maksimal("Evaluasi", Maks);
        RuleFor(x => x.Catatan).Maksimal("Catatan", Maks);
    }
}

public class CreateReportDtoValidator : ReportInputValidator<CreateReportDto>;

public class UpdateReportDtoValidator : ReportInputValidator<UpdateReportDto>;
