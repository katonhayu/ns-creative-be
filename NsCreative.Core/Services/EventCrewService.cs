using System.Linq.Expressions;
using NsCreative.Core.Common;
using NsCreative.Core.Dtos.EventCrews;
using NsCreative.Core.Entities;
using NsCreative.Core.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace NsCreative.Core.Services;

public interface IEventCrewService
{
    Task<PagedResult<EventCrewDto>> GetListAsync(Guid eventId, QueryParams query, CancellationToken ct = default);
    Task<EventCrewDto> GetByIdAsync(Guid eventId, Guid id, CancellationToken ct = default);
    Task<EventCrewDto> CreateAsync(Guid eventId, CreateEventCrewDto dto, CancellationToken ct = default);
    Task<EventCrewDto> UpdateAsync(Guid eventId, Guid id, UpdateEventCrewDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid eventId, Guid id, CancellationToken ct = default);

    /// <summary>Crew yang boleh dipilih sebagai PIC task dan sesi rundown: PIC event, lalu crew yang ditugaskan.</summary>
    Task<IReadOnlyList<PenanggungJawabDto>> GetPenanggungJawabAsync(Guid eventId, CancellationToken ct = default);
}

/// <summary>
/// Penugasan crew ke event. Tidak ada budget otomatis: honor crew dicatat manual sebagai
/// satu baris budget borongan. Bentrok jadwal hanya peringatan, tidak memblokir.
/// </summary>
public class EventCrewService(
    IAppDbContext db,
    IAksesEvent akses,
    IValidator<CreateEventCrewDto> createValidator,
    IValidator<UpdateEventCrewDto> updateValidator) : IEventCrewService
{
    private static readonly PetaUrutan<EventCrew> Urutan = new PetaUrutan<EventCrew>()
        .Kolom("tanggalBertugas", ec => ec.TanggalBertugas, bawaan: true, nullDiBawah: true, lanjutan: ec => ec.Crew.Nama)
        .Kolom("nama", ec => ec.Crew.Nama)
        .Kolom("posisi", ec => ec.Posisi, lanjutan: ec => ec.Crew.Nama)
        .Kolom("createdAt", ec => ec.CreatedAt);

    /// <summary>Hasil query sebelum dilengkapi daftar bentrok.</summary>
    private sealed record Baris(
        Guid Id, Guid EventId, Guid CrewId, string NamaCrew, string? Keahlian, string? Telepon,
        string Posisi, DateOnly? TanggalBertugas, DateTime CreatedAt, DateTime UpdatedAt);

    private static readonly Expression<Func<EventCrew, Baris>> KeBaris = ec => new Baris(
        ec.Id, ec.EventId, ec.CrewId, ec.Crew.Nama, ec.Crew.Keahlian, ec.Crew.Telepon,
        ec.Posisi, ec.TanggalBertugas, ec.CreatedAt, ec.UpdatedAt);

    public async Task<PagedResult<EventCrewDto>> GetListAsync(
        Guid eventId, QueryParams query, CancellationToken ct = default)
    {
        var ev = await akses.LihatAsync(eventId, ct);
        var halaman = await Penugasan(eventId)
            .Cari(query.Search, ec => ec.Crew.Nama, ec => ec.Crew.Keahlian, ec => ec.Posisi)
            .Urutkan(query, Urutan)
            .Select(KeBaris)
            .ToPagedResultAsync(query, ct);

        var items = await LengkapiAsync(ev, halaman.Items, ct);
        return new PagedResult<EventCrewDto>(items, halaman.Page, halaman.PageSize, halaman.TotalItems, halaman.TotalPages);
    }

    public async Task<EventCrewDto> GetByIdAsync(Guid eventId, Guid id, CancellationToken ct = default)
    {
        var ev = await akses.LihatAsync(eventId, ct);
        var baris = await Penugasan(eventId).Where(ec => ec.Id == id).Select(KeBaris).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Penugasan crew tidak ditemukan.");
        return (await LengkapiAsync(ev, [baris], ct))[0];
    }

    /// <summary>Respons membawa daftar bentrok supaya frontend bisa langsung menampilkan peringatan.</summary>
    public async Task<EventCrewDto> CreateAsync(Guid eventId, CreateEventCrewDto dto, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(dto, ct);
        await akses.UbahAsync(eventId, ct);

        var namaCrew = await db.Crews.AsNoTracking()
            .Where(c => c.Id == dto.CrewId)
            .Select(c => c.Nama)
            .FirstOrDefaultAsync(ct)
            ?? throw new BusinessRuleException("Crew yang dipilih tidak ditemukan atau sudah dihapus.");
        if (await db.EventCrews.AnyAsync(ec => ec.EventId == eventId && ec.CrewId == dto.CrewId, ct))
            throw new BusinessRuleException($"{namaCrew} sudah ditugaskan di event ini.");

        var penugasan = new EventCrew { EventId = eventId, CrewId = dto.CrewId };
        Terapkan(penugasan, dto);
        db.EventCrews.Add(penugasan);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(eventId, penugasan.Id, ct);
    }

    public async Task<EventCrewDto> UpdateAsync(Guid eventId, Guid id, UpdateEventCrewDto dto, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(dto, ct);
        await akses.UbahAsync(eventId, ct);
        var penugasan = await CariAsync(eventId, id, ct);

        Terapkan(penugasan, dto);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(eventId, id, ct);
    }

    /// <summary>
    /// Ditolak jika crew masih menjadi PIC task atau sesi rundown di event ini, karena PIC keduanya
    /// harus crew yang terlibat. PIC event tetap terlibat walau penugasannya dihapus.
    /// </summary>
    public async Task DeleteAsync(Guid eventId, Guid id, CancellationToken ct = default)
    {
        var ev = await akses.UbahAsync(eventId, ct);
        var penugasan = await CariAsync(eventId, id, ct);
        if (penugasan.CrewId != ev.PicCrewId)
        {
            var crewId = penugasan.CrewId;
            var jumlahTask = await db.Tasks.CountAsync(t => t.EventId == eventId && t.PicCrewId == crewId, ct);
            var jumlahSesi = await db.Schedules.CountAsync(s => s.EventId == eventId && s.PicCrewId == crewId, ct);
            if (jumlahTask + jumlahSesi > 0)
            {
                var nama = await db.Crews.IgnoreQueryFilters().Where(c => c.Id == crewId).Select(c => c.Nama).FirstAsync(ct);
                var dipegang = string.Join(" dan ", new[]
                {
                    jumlahTask > 0 ? $"{jumlahTask} task" : null,
                    jumlahSesi > 0 ? $"{jumlahSesi} sesi rundown" : null,
                }.OfType<string>());
                throw new BusinessRuleException(
                    $"{nama} masih menjadi PIC {dipegang} di event ini. Pindahkan PIC-nya terlebih dahulu.");
            }
        }

        db.EventCrews.Remove(penugasan);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PenanggungJawabDto>> GetPenanggungJawabAsync(Guid eventId, CancellationToken ct = default)
    {
        var ev = await akses.LihatAsync(eventId, ct);
        var pic = await db.Crews.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => c.Id == ev.PicCrewId)
            .Select(c => new PenanggungJawabDto(c.Id, c.Nama, "PIC event", c.UserId != null))
            .FirstAsync(ct);
        var ditugaskan = await Penugasan(eventId)
            .Where(ec => ec.CrewId != ev.PicCrewId)
            .OrderBy(ec => ec.Crew.Nama)
            .Select(ec => new PenanggungJawabDto(ec.CrewId, ec.Crew.Nama, ec.Posisi, ec.Crew.UserId != null))
            .ToListAsync(ct);
        return [pic, .. ditugaskan];
    }

    /// <summary>Filter soft delete diabaikan supaya crew yang sudah dihapus tetap tampil di event lama.</summary>
    private IQueryable<EventCrew> Penugasan(Guid eventId) =>
        db.EventCrews.AsNoTracking().IgnoreQueryFilters().Where(ec => ec.EventId == eventId);

    /// <summary>Tambahkan daftar bentrok ke setiap baris dengan satu query.</summary>
    private async Task<IReadOnlyList<EventCrewDto>> LengkapiAsync(
        InfoEvent ev, IReadOnlyList<Baris> daftar, CancellationToken ct)
    {
        var rentang = daftar
            .Select(b => new BentrokCrew.Rentang(
                b.CrewId, b.TanggalBertugas ?? ev.TanggalMulai, b.TanggalBertugas ?? ev.TanggalSelesai))
            .ToList();
        var bentrok = await BentrokCrew.CariAsync(db, rentang, ev.Id, ct);

        return daftar
            .Select(b => new EventCrewDto(
                b.Id, b.EventId, b.CrewId, b.NamaCrew, b.Keahlian, b.Telepon, b.Posisi, b.TanggalBertugas,
                bentrok[b.CrewId].ToList(), b.CreatedAt, b.UpdatedAt))
            .ToList();
    }

    private async Task<EventCrew> CariAsync(Guid eventId, Guid id, CancellationToken ct) =>
        await db.EventCrews.FirstOrDefaultAsync(ec => ec.Id == id && ec.EventId == eventId, ct)
        ?? throw new NotFoundException("Penugasan crew tidak ditemukan.");

    private static void Terapkan(EventCrew penugasan, IEventCrewInput dto)
    {
        penugasan.Posisi = Teks.Wajib(dto.Posisi);
        penugasan.TanggalBertugas = dto.TanggalBertugas;
    }
}
