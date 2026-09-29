using NsCreative.Core.Dtos.Schedules;
using FluentValidation;

namespace NsCreative.Core.Validators.Schedules;

/// <summary>
/// Aturan bersama create, bulk, dan update sesi rundown. Tanggal di luar rentang event
/// bukan error, hanya ditandai di respons.
/// </summary>
public abstract class ScheduleInputValidator<T> : AbstractValidator<T> where T : IScheduleInput
{
    protected ScheduleInputValidator()
    {
        RuleFor(x => x.Tanggal).NotNull().WithMessage("Tanggal harus diisi.");
        RuleFor(x => x.WaktuMulai).NotNull().WithMessage("Waktu mulai harus diisi.");
        // Database mensyaratkan waktu_selesai > waktu_mulai.
        RuleFor(x => x.WaktuSelesai)
            .GreaterThan(x => x.WaktuMulai!.Value).WithMessage("Waktu selesai harus setelah waktu mulai.")
            .When(x => x.WaktuSelesai.HasValue && x.WaktuMulai.HasValue);
        RuleFor(x => x.Kegiatan).Wajib("Kegiatan", 200);
        RuleFor(x => x.Lokasi).Maksimal("Lokasi", 200);
        RuleFor(x => x.PicUserId)
            .Must(id => id != Guid.Empty).WithMessage("PIC tidak valid.")
            .When(x => x.PicUserId.HasValue);
    }
}

public class CreateScheduleDtoValidator : ScheduleInputValidator<CreateScheduleDto>;

public class UpdateScheduleDtoValidator : ScheduleInputValidator<UpdateScheduleDto>;
