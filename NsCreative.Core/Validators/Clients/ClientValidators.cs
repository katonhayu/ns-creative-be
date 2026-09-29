using NsCreative.Core.Dtos.Clients;
using FluentValidation;

namespace NsCreative.Core.Validators.Clients;

/// <summary>Aturan bersama create dan update client, sesuai panjang kolom tabel clients.</summary>
public abstract class ClientInputValidator<T> : AbstractValidator<T> where T : IClientInput
{
    protected ClientInputValidator()
    {
        RuleFor(x => x.Nama).Wajib("Nama", 100);
        RuleFor(x => x.Perusahaan).Maksimal("Perusahaan", 150);
        RuleFor(x => x.Telepon).TeleponOpsional();
        RuleFor(x => x.Email).EmailOpsional();
        RuleFor(x => x.Alamat).Maksimal("Alamat", 500);
    }
}

public class CreateClientDtoValidator : ClientInputValidator<CreateClientDto>;

public class UpdateClientDtoValidator : ClientInputValidator<UpdateClientDto>;
