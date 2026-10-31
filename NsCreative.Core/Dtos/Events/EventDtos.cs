using NsCreative.Core.Enums;

namespace NsCreative.Core.Dtos.Events;

/// <param name="Deskripsi">Ikut dikirim supaya form edit dari halaman daftar tidak mengosongkannya.</param>
/// <param name="PicCrewId">Crew penanggung jawab event.</param>
/// <param name="BolehKelola">Pengguna login boleh mengubah event ini (admin, atau crew yang menjadi PIC-nya).</param>
public record EventListDto(
    Guid Id,
    string Nama,
    string Jenis,
    DateOnly TanggalMulai,
    DateOnly? TanggalSelesai,
    string? Lokasi,
    string? Deskripsi,
    EventStatus Status,
    Guid ClientId,
    string NamaClient,
    string? PerusahaanClient,
    Guid PicCrewId,
    string NamaPic,
    bool BolehKelola);

/// <summary>Detail event beserta kontak client dan PIC.</summary>
/// <param name="StatusBerikutnya">Status yang boleh dipilih lewat PATCH status. Kosong untuk selesai/dibatalkan.</param>
/// <param name="EmailPic">Email akun login crew PIC. Null jika tautan akunnya sudah dilepas.</param>
/// <param name="BolehKelola">
/// Pengguna login boleh mengubah event ini dan data di bawahnya (admin, atau crew yang menjadi PIC-nya).
/// False berarti hanya bisa melihat, kecuali mengubah status task miliknya sendiri.
/// </param>
public record EventDto(
    Guid Id,
    string Nama,
    string Jenis,
    DateOnly TanggalMulai,
    DateOnly? TanggalSelesai,
    string? Lokasi,
    string? Deskripsi,
    EventStatus Status,
    IReadOnlyList<EventStatus> StatusBerikutnya,
    Guid ClientId,
    string NamaClient,
    string? PerusahaanClient,
    string? TeleponClient,
    string? EmailClient,
    Guid PicCrewId,
    string NamaPic,
    string? TeleponPic,
    string? EmailPic,
    bool BolehKelola,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record EventLookupDto(
    Guid Id,
    string Nama,
    DateOnly TanggalMulai,
    DateOnly? TanggalSelesai,
    EventStatus Status,
    string NamaClient);

/// <summary>Filter tambahan daftar event. Semua opsional.</summary>
public class EventFilter
{
    public EventStatus? Status { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? PicCrewId { get; set; }

    /// <summary>Awal rentang. Event yang sebagian harinya beririsan dengan rentang ikut tampil.</summary>
    public DateOnly? DariTanggal { get; set; }

    /// <summary>Akhir rentang (inklusif).</summary>
    public DateOnly? SampaiTanggal { get; set; }
}

/// <summary>Ringkasan untuk tab Ringkasan di halaman detail event.</summary>
/// <param name="HariMenujuEvent">Selisih hari dari hari ini (WIB) ke tanggal mulai. Negatif jika sudah lewat.</param>
public record EventSummaryDto(
    Guid EventId,
    string Nama,
    EventStatus Status,
    int HariMenujuEvent,
    RingkasanTaskDto Task,
    RingkasanBudgetDto Budget,
    int JumlahTalent,
    int JumlahCrew,
    int JumlahVendor,
    int JumlahDokumen);

/// <summary>Terlambat = deadline sebelum hari ini dan status belum selesai (tidak disimpan).</summary>
public record RingkasanTaskDto(int Total, int Selesai, int Terlambat, double PersenSelesai);

/// <summary>Selisih = totalEstimasi - totalAktual. BelumLunas = jumlah baris budget yang belum lunas.</summary>
public record RingkasanBudgetDto(
    decimal TotalEstimasi,
    decimal TotalAktual,
    decimal Selisih,
    decimal TotalSponsorship,
    int BelumLunas);

public interface IEventInput
{
    Guid ClientId { get; }

    /// <summary>
    /// Crew PIC. Saat crew membuat event, boleh kosong karena otomatis diisi crew miliknya.
    /// Admin wajib mengisinya saat membuat. Saat update, kosong berarti PIC tidak diubah.
    /// </summary>
    Guid? PicCrewId { get; }
    string Nama { get; }
    string Jenis { get; }
    DateOnly TanggalMulai { get; }
    DateOnly? TanggalSelesai { get; }
    string? Lokasi { get; }
    string? Deskripsi { get; }
}

/// <summary>Status awal selalu draft.</summary>
public record CreateEventDto(
    Guid ClientId,
    Guid? PicCrewId,
    string Nama,
    string Jenis,
    DateOnly TanggalMulai,
    DateOnly? TanggalSelesai,
    string? Lokasi,
    string? Deskripsi) : IEventInput;

/// <summary>Status tidak bisa diubah lewat sini; pakai PATCH status. Hanya admin yang boleh mengganti PIC.</summary>
public record UpdateEventDto(
    Guid ClientId,
    Guid? PicCrewId,
    string Nama,
    string Jenis,
    DateOnly TanggalMulai,
    DateOnly? TanggalSelesai,
    string? Lokasi,
    string? Deskripsi) : IEventInput;

public record UpdateEventStatusDto(EventStatus? Status);
