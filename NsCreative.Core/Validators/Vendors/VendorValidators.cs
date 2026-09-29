using NsCreative.Core.Dtos.Vendors;
using FluentValidation;

namespace NsCreative.Core.Validators.Vendors;

/// <summary>Aturan bersama create dan update vendor, sesuai panjang kolom tabel vendors.</summary>
public abstract class VendorInputValidator<T> : AbstractValidator<T> where T : IVendorInput
{
    protected VendorInputValidator()
    {
        RuleFor(x => x.Nama).Wajib("Nama vendor", 100);
        RuleFor(x => x.JenisJasa).Wajib("Jenis jasa", 100);
        RuleFor(x => x.ContactPerson).Maksimal("Contact person", 100);
        RuleFor(x => x.Telepon).TeleponOpsional();
    }
}

public class CreateVendorDtoValidator : VendorInputValidator<CreateVendorDto>;

public class UpdateVendorDtoValidator : VendorInputValidator<UpdateVendorDto>;
