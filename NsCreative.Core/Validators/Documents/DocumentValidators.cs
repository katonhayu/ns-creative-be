using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Documents;
using FluentValidation;

namespace NsCreative.Core.Validators.Documents;

/// <summary>Kecocokan isi file dengan formatnya dicek di service karena perlu membaca stream.</summary>
public class UploadDocumentDtoValidator : AbstractValidator<UploadDocumentDto>
{
    public UploadDocumentDtoValidator()
    {
        RuleFor(x => x.NamaDokumen).Wajib("Nama dokumen", 150);
        RuleFor(x => x.Jenis).Wajib("Jenis dokumen", 50);
        RuleFor(x => x.Isi).NotNull().WithMessage("File harus dipilih.").OverridePropertyName("File");

        When(x => x.Isi is not null, () =>
        {
            RuleFor(x => x.Ukuran)
                .GreaterThan(0).WithMessage("File kosong.")
                .LessThanOrEqualTo(TipeDokumen.UkuranMaks).WithMessage("Ukuran file maksimal 10 MB.")
                .OverridePropertyName("File");
            RuleFor(x => x.NamaFileAsli)
                .Must(n => TipeDokumen.Diizinkan(Path.GetExtension(n)))
                .WithMessage($"Tipe file tidak diizinkan. Pilihan: {TipeDokumen.DaftarEkstensi}.")
                .OverridePropertyName("File");
        });
    }
}

public class UpdateDocumentDtoValidator : AbstractValidator<UpdateDocumentDto>
{
    public UpdateDocumentDtoValidator()
    {
        RuleFor(x => x.NamaDokumen).Wajib("Nama dokumen", 150);
        RuleFor(x => x.Jenis).Wajib("Jenis dokumen", 50);
        RuleFor(x => x.Tanggal).NotNull().WithMessage("Tanggal harus diisi.");
    }
}
