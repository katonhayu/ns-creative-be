using NsCreative.Core.Dtos.Events;
using FluentValidation;

namespace NsCreative.Core.Validators.Events;

/// <summary>
/// Aturan bersama create dan update event. Keberadaan client dan PIC dicek di service
/// karena butuh database.
/// </summary>
public abstract class EventInputValidator<T> : AbstractValidator<T> where T : IEventInput
{
    protected EventInputValidator()
    {
        RuleFor(x => x.ClientId).NotEqual(Guid.Empty).WithMessage("Client harus dipilih.");
        RuleFor(x => x.PicUserId).NotEqual(Guid.Empty).WithMessage("PIC harus dipilih.");
        RuleFor(x => x.Nama).Wajib("Nama event", 150);
        RuleFor(x => x.Jenis).Wajib("Jenis event", 50);
        RuleFor(x => x.TanggalMulai).NotEqual(default(DateOnly)).WithMessage("Tanggal mulai harus diisi.");
        RuleFor(x => x.TanggalSelesai)
            .GreaterThanOrEqualTo(x => x.TanggalMulai)
            .WithMessage("Tanggal selesai tidak boleh lebih awal dari tanggal mulai.")
            .When(x => x.TanggalSelesai.HasValue);
        RuleFor(x => x.Lokasi).Maksimal("Lokasi", 200);
    }
}

public class CreateEventDtoValidator : EventInputValidator<CreateEventDto>;

public class UpdateEventDtoValidator : EventInputValidator<UpdateEventDto>;

/// <summary>Keabsahan transisi status dicek di service karena bergantung status saat ini.</summary>
public class UpdateEventStatusDtoValidator : AbstractValidator<UpdateEventStatusDto>
{
    public UpdateEventStatusDtoValidator()
    {
        RuleFor(x => x.Status)
            .NotNull().WithMessage("Status harus diisi.")
            .IsInEnum().WithMessage("Status event tidak dikenal.");
    }
}
