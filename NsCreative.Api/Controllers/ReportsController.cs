using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Reports;
using NsCreative.Core.Services;
using NsCreative.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace NsCreative.Api.Controllers;

/// <summary>Laporan evaluasi event. Satu laporan per event, hanya untuk event yang sudah selesai.</summary>
[ApiController]
[Authorize(Roles = Peran.AdminPic)]
[Route("api")]
[Produces("application/json")]
public class ReportsController(IReportService reports) : ControllerBase
{
    /// <summary>Daftar semua laporan (khusus admin).</summary>
    /// <remarks>sortBy: tanggalDibuat (default, terbaru dulu), tanggalMulai, namaEvent, totalPengeluaran. Filter <c>tahun</c> = tahun tanggal mulai event.</remarks>
    [Authorize(Roles = Peran.Admin)]
    [HttpGet("reports")]
    [ProducesResponseType<PagedResult<ReportListDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ReportListDto>>> GetList(
        [FromQuery] QueryParams query, [FromQuery] int? tahun, CancellationToken ct) =>
        Ok(await reports.GetListAsync(query, tahun, ct));

    /// <summary>Event yang sudah selesai beserta laporannya (null jika belum dibuat). Tidak berpaginasi.</summary>
    /// <remarks>Untuk halaman Laporan. Pic hanya melihat event miliknya. Filter <c>tahun</c> = tahun tanggal mulai event.</remarks>
    [HttpGet("reports/events")]
    [ProducesResponseType<IReadOnlyList<LaporanEventDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LaporanEventDto>>> GetEventSelesai(
        [FromQuery] int? tahun, CancellationToken ct) =>
        Ok(await reports.GetEventSelesaiAsync(tahun, ct));

    /// <summary>Laporan event. 404 jika belum dibuat.</summary>
    [HttpGet("events/{eventId:guid}/report")]
    [ProducesResponseType<ReportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReportDto>> GetByEvent(Guid eventId, CancellationToken ct) =>
        Ok(await reports.GetByEventAsync(eventId, ct));

    /// <summary>⚡ Generate laporan. Total pengeluaran = jumlah biaya aktual budget saat ini, lalu dibekukan.</summary>
    /// <remarks>Ditolak 422 jika status event belum selesai atau laporan sudah ada. Body opsional (isi naratif awal).</remarks>
    [HttpPost("events/{eventId:guid}/report")]
    [ProducesResponseType<ReportDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReportDto>> Create(
        Guid eventId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateReportDto? dto,
        CancellationToken ct)
    {
        var hasil = await reports.CreateAsync(eventId, dto, ct);
        return CreatedAtAction(nameof(GetByEvent), new { eventId }, hasil);
    }

    /// <summary>Ubah isi naratif. <c>totalPengeluaran</c> tidak bisa diubah.</summary>
    [HttpPut("events/{eventId:guid}/report")]
    [ProducesResponseType<ReportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReportDto>> Update(Guid eventId, UpdateReportDto dto, CancellationToken ct) =>
        Ok(await reports.UpdateAsync(eventId, dto, ct));
}
