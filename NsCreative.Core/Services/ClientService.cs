using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Clients;
using NsCreative.Core.Entities;
using NsCreative.Core.Enums;
using NsCreative.Core.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace NsCreative.Core.Services;

public interface IClientService
{
    Task<PagedResult<ClientListDto>> GetListAsync(QueryParams query, CancellationToken ct = default);
    Task<ClientDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ClientLookupDto>> GetLookupAsync(string? search, CancellationToken ct = default);
    Task<PagedResult<ClientEventDto>> GetEventsAsync(Guid id, QueryParams query, CancellationToken ct = default);
    Task<ClientDto> CreateAsync(CreateClientDto dto, CancellationToken ct = default);
    Task<ClientDto> UpdateAsync(Guid id, UpdateClientDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class ClientService(
    IAppDbContext db,
    IValidator<CreateClientDto> createValidator,
    IValidator<UpdateClientDto> updateValidator) : IClientService
{
    private static readonly PetaUrutan<Client> Urutan = new PetaUrutan<Client>()
        .Kolom("nama", c => c.Nama, bawaan: true)
        .Kolom("perusahaan", c => c.Perusahaan, nullDiBawah: true, lanjutan: c => c.Nama)
        .Kolom("createdAt", c => c.CreatedAt);

    public async Task<PagedResult<ClientListDto>> GetListAsync(QueryParams query, CancellationToken ct = default) =>
        await db.Clients.AsNoTracking()
            .Cari(query.Search, c => c.Nama, c => c.Perusahaan)
            .Urutkan(query, Urutan)
            .Select(c => new ClientListDto(
                c.Id, c.Nama, c.Perusahaan, c.Telepon, c.Email, c.Alamat,
                db.Events.Count(e => e.ClientId == c.Id)))
            .ToPagedResultAsync(query, ct);

    public async Task<ClientDto> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Clients.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new ClientDto(
                c.Id, c.Nama, c.Perusahaan, c.Telepon, c.Email, c.Alamat,
                db.Events.Count(e => e.ClientId == c.Id),
                c.CreatedAt, c.UpdatedAt))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Client tidak ditemukan.");

    public async Task<IReadOnlyList<ClientLookupDto>> GetLookupAsync(string? search, CancellationToken ct = default) =>
        await db.Clients.AsNoTracking()
            .Cari(search, c => c.Nama, c => c.Perusahaan)
            .OrderBy(c => c.Nama)
            .Take(QueryParams.LookupMaks)
            .Select(c => new ClientLookupDto(c.Id, c.Nama, c.Perusahaan))
            .ToListAsync(ct);

    public async Task<PagedResult<ClientEventDto>> GetEventsAsync(Guid id, QueryParams query, CancellationToken ct = default)
    {
        // Riwayat tetap bisa dibuka walau client sudah dihapus, karena event lama tetap tersimpan.
        if (!await db.Clients.IgnoreQueryFilters().AnyAsync(c => c.Id == id, ct))
            throw new NotFoundException("Client tidak ditemukan.");

        var q = db.Events.AsNoTracking()
            .Where(e => e.ClientId == id)
            .Cari(query.Search, e => e.Nama);
        // Riwayat default terbaru lebih dulu; sortDir=asc untuk urutan terlama.
        q = string.Equals(query.SortDir, "asc", StringComparison.OrdinalIgnoreCase)
            ? q.OrderBy(e => e.TanggalMulai)
            : q.OrderByDescending(e => e.TanggalMulai);

        return await q
            .Select(e => new ClientEventDto(e.Id, e.Nama, e.Jenis, e.TanggalMulai, e.TanggalSelesai, e.Lokasi, e.Status))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<ClientDto> CreateAsync(CreateClientDto dto, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(dto, ct);

        var client = new Client();
        Terapkan(client, dto);
        db.Clients.Add(client);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(client.Id, ct);
    }

    public async Task<ClientDto> UpdateAsync(Guid id, UpdateClientDto dto, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(dto, ct);

        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Client tidak ditemukan.");
        Terapkan(client, dto);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(client.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Client tidak ditemukan.");

        var jumlahAktif = await db.Events.CountAsync(
            e => e.ClientId == id && EventStatusExtensions.Aktif.Contains(e.Status), ct);
        if (jumlahAktif > 0)
        {
            throw new BusinessRuleException(
                $"Client {client.Nama} masih punya {jumlahAktif} event aktif. " +
                "Selesaikan atau batalkan event tersebut sebelum menghapus client.");
        }

        // Soft delete: data event lama yang memakai client ini tetap utuh.
        client.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static void Terapkan(Client client, IClientInput dto)
    {
        client.Nama = Teks.Wajib(dto.Nama);
        client.Perusahaan = Teks.Bersihkan(dto.Perusahaan);
        client.Telepon = Teks.Bersihkan(dto.Telepon);
        client.Email = Teks.Email(dto.Email);
        client.Alamat = Teks.Bersihkan(dto.Alamat);
    }
}
