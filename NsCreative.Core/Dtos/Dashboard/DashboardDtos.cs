using NsCreative.Core.Enums;

namespace NsCreative.Core.Dtos.Dashboard;

/// <summary>Ringkasan halaman depan.</summary>
public record DashboardDto(
    IReadOnlyList<DashboardEventBerlangsungDto> EventBerlangsung,
    IReadOnlyList<DashboardEventTerdekatDto> EventTerdekat,
    IReadOnlyList<DashboardTaskDto> TaskMendekatiDeadline,
    IReadOnlyList<DashboardVendorDto> VendorBelumLunas,
    DashboardStatistikDto Statistik);

/// <param name="PersenTask">Persentase task selesai, satu desimal.</param>
public record DashboardEventBerlangsungDto(
    Guid Id,
    string Nama,
    DateOnly TanggalMulai,
    DateOnly? TanggalSelesai,
    string? Lokasi,
    EventStatus Status,
    string NamaPic,
    double PersenTask);

public record DashboardEventTerdekatDto(
    Guid Id,
    string Nama,
    DateOnly TanggalMulai,
    DateOnly? TanggalSelesai,
    string? Lokasi,
    EventStatus Status,
    int HariLagi);

public record DashboardTaskDto(
    Guid Id,
    Guid EventId,
    string NamaTugas,
    DateOnly Deadline,
    TaskItemStatus Status,
    string EventNama,
    string? NamaPic,
    bool IsTerlambat);

public record DashboardVendorDto(
    Guid EventVendorId,
    Guid EventId,
    string VendorNama,
    string EventNama,
    string? Kebutuhan,
    decimal Biaya,
    StatusPembayaran StatusPembayaran);

/// <param name="TotalEventTahunIni">Event tahun ini (tanggal mulai), tidak termasuk yang dibatalkan.</param>
/// <param name="TotalEventSelesai">Event tahun ini yang berstatus selesai.</param>
/// <param name="TotalClientAktif">Jumlah client berbeda yang punya event tahun ini (tidak termasuk yang dibatalkan).</param>
public record DashboardStatistikDto(int TotalEventTahunIni, int TotalEventSelesai, int TotalClientAktif);
