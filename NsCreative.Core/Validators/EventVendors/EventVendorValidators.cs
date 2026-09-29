using NsCreative.Core.Dtos.EventVendors;
using FluentValidation;

namespace NsCreative.Core.Validators.EventVendors;

public abstract class EventVendorInputValidator<T> : AbstractValidator<T> where T : IEventVendorInput
{
    protected EventVendorInputValidator()
    {
        RuleFor(x => x.Kebutuhan).Maksimal("Kebutuhan", 500);
        RuleFor(x => x.Biaya).Nominal("Biaya");
    }
}

public class CreateEventVendorDtoValidator : EventVendorInputValidator<CreateEventVendorDto>
{
    public CreateEventVendorDtoValidator()
    {
        RuleFor(x => x.VendorId).NotEqual(Guid.Empty).WithMessage("Vendor harus dipilih.");
        RuleFor(x => x.StatusPembayaran)
            .IsInEnum().WithMessage("Status pembayaran tidak dikenal.")
            .When(x => x.StatusPembayaran.HasValue);
    }
}

public class UpdateEventVendorDtoValidator : EventVendorInputValidator<UpdateEventVendorDto>;

public class UpdateEventVendorPembayaranDtoValidator : AbstractValidator<UpdateEventVendorPembayaranDto>
{
    public UpdateEventVendorPembayaranDtoValidator()
    {
        RuleFor(x => x.StatusPembayaran)
            .NotNull().WithMessage("Status pembayaran harus diisi.")
            .IsInEnum().WithMessage("Status pembayaran tidak dikenal.");
    }
}
