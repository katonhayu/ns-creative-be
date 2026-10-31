using NsCreative.Core.Dtos.Tasks;
using NsCreative.Core.Enums;
using FluentValidation;

namespace NsCreative.Core.Validators.Tasks;

public abstract class TaskInputValidator<T> : AbstractValidator<T> where T : ITaskInput
{
    protected TaskInputValidator()
    {
        RuleFor(x => x.NamaTugas).Wajib("Nama tugas", 150);
        RuleFor(x => x.PicCrewId)
            .Must(id => id != Guid.Empty).WithMessage("PIC tidak valid.")
            .When(x => x.PicCrewId.HasValue);
    }
}

public class CreateTaskDtoValidator : TaskInputValidator<CreateTaskDto>;

public class UpdateTaskDtoValidator : TaskInputValidator<UpdateTaskDto>;

public class UpdateTaskStatusDtoValidator : AbstractValidator<UpdateTaskStatusDto>
{
    public UpdateTaskStatusDtoValidator()
    {
        RuleFor(x => x.Status)
            .NotNull().WithMessage("Status harus diisi.")
            .IsInEnum().WithMessage("Status task tidak dikenal.")
            .NotEqual(TaskItemStatus.Terlambat)
            .WithMessage("Status terlambat dihitung otomatis dari deadline, tidak bisa dipilih.");
    }
}
