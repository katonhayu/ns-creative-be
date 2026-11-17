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
[Authorize(Roles = Peran.Semua)]
[Route("api")]
[Produces("application/json")]
public class ReportsController(IReportService reports) : ControllerBase
{
    /// <summary>Daftar semua laporan (khusus admin).</summary>
    /// <remarks>sortBy: tanggalDibuat (default, terbaru dulu), tanggalMulai, namaEvent, totalPengeluaran. Filter <c>tahun</c> = tahun tanggal mulai event.</remarks>
    [Authorize(Roles = Peran.Pemantau)]
    [HttpGet("reports")]
    [ProducesResponseType<PagedResult<ReportListDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ReportListDto>>> GetList(
        [FromQuery] QueryParams query, [FromQuery] int? tahun, CancellationToken ct) =>
        Ok(await reports.GetListAsync(query, tahun, ct));

    /// <summary>Event yang sudah selesai beserta laporannya (null jika belum dibuat). Tidak berpaginasi.</summary>
    /// <remarks>Untuk halaman Laporan. Crew hanya melihat event yang ia pegang atau tempat ia ditugaskan. Filter <c>tahun</c> = tahun tanggal mulai event.</remarks>
    [HttpGet("reports/events")]
    [ProducesResponseType<IReadOnlyList<LaporanEventDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LaporanEventDto>>> GetEventSelesai(
        [FromQuery] int? tahun, CancellationToken ct) =>
        Ok(await reports.GetEventSelesaiAsync(tahun, ct));
    /// <summary>PDF rekap event selesai pada satu periode (landscape A4).</summary>
    /// <summary>PDF rekap event selesai sesuai filter halaman Laporan (landscape A4).</summary>
    /// <remarks>
    /// Filter opsional: <c>dariTanggal</c> dan <c>sampaiTanggal</c> ("2026-01-01", inklusif, dicocokkan dengan
    /// tanggal selesai event), <c>sudahDibuat</c> (true/false), <c>search</c> (nama event atau client).
    /// Crew hanya mendapat event yang ia pegang atau tempat ia ditugaskan.
    /// </remarks>
    [HttpGet("reports/rekap/pdf")]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK, "application/pdf")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRekapPdf([FromQuery] RekapLaporanFilter filter, CancellationToken ct)
    {
        var file = await reports.PdfRekapAsync(filter, ct);
        return File(file.Isi, "application/pdf", file.NamaFile);
    }

    /// <summary>PDF laporan pertanggungjawaban event (A4). 404 jika laporan belum dibuat.</summary>
    /// <remarks>
    /// Isi: data event, hasil pelaksanaan, ringkasan dan rincian anggaran, talent, vendor, crew,
    /// rundown, ringkasan task, kendala, evaluasi, daftar dokumen, dan kolom tanda tangan.
    /// </remarks>
    [HttpGet("events/{eventId:guid}/report/pdf")]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK, "application/pdf")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPdf(Guid eventId, CancellationToken ct)
    {
        var file = await reports.PdfLaporanEventAsync(eventId, ct);
        return File(file.Isi, "application/pdf", file.NamaFile);
    }

    /// <summary>Laporan event. 404 jika belum dibuat.</summary>
    [HttpGet("events/{eventId:guid}/report")]
    [ProducesResponseType<ReportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReportDto>> GetByEvent(Guid eventId, CancellationToken ct) =>
        Ok(await reports.GetByEventAsync(eventId, ct));

    /// <summary>⚡ Generate laporan. Total pengeluaran = jumlah biaya aktual budget saat ini, lalu dibekukan.</summary>
    /// <remarks>Ditolak 422 jika status event belum selesai atau laporan sudah ada. Body opsional (isi naratif awal).</remarks>
    [Authorize(Roles = Peran.Pengelola)]
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
    [Authorize(Roles = Peran.Pengelola)]
    [HttpPut("events/{eventId:guid}/report")]
    [ProducesResponseType<ReportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReportDto>> Update(Guid eventId, UpdateReportDto dto, CancellationToken ct) =>
        Ok(await reports.UpdateAsync(eventId, dto, ct));
}
