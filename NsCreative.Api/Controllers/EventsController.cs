using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Events;
using NsCreative.Core.Enums;
using NsCreative.Core.Services;
using NsCreative.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NsCreative.Api.Controllers;

/// <summary>Event, pusat data transaksional. Error ditangani ExceptionHandlingMiddleware.</summary>
/// <remarks>Difilter otomatis per peran: pic hanya event miliknya, crew hanya event tempat ia bertugas.</remarks>
[ApiController]
[Authorize]
[Route("api/events")]
[Produces("application/json")]
public class EventsController(IEventService events) : ControllerBase
{
    /// <summary>Daftar event berpaginasi. Pencarian pada nama, jenis, lokasi, dan client.</summary>
    /// <remarks>
    /// sortBy: tanggalMulai (default), nama, status (urut alur status), createdAt.
    /// Filter: status, clientId, picUserId, dariTanggal, sampaiTanggal (event yang beririsan dengan rentang ikut tampil).
    /// </remarks>
    [Authorize(Roles = Peran.Semua)]
    [HttpGet]
    [ProducesResponseType<PagedResult<EventListDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<EventListDto>>> GetList(
        [FromQuery] QueryParams query, [FromQuery] EventFilter filter, CancellationToken ct) =>
        Ok(await events.GetListAsync(query, filter, ct));

    /// <summary>Pilihan event untuk dropdown (maksimal 50, terbaru lebih dulu).</summary>
    [Authorize(Roles = Peran.AdminPic)]
    [HttpGet("lookup")]
    [ProducesResponseType<IReadOnlyList<EventLookupDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EventLookupDto>>> GetLookup(
        [FromQuery] string? search, [FromQuery] EventStatus? status, CancellationToken ct) =>
        Ok(await events.GetLookupAsync(search, status, ct));

    /// <summary>Detail event beserta nama client, nama PIC, dan status berikutnya yang diizinkan.</summary>
    [Authorize(Roles = Peran.Semua)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType<EventDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await events.GetByIdAsync(id, ct));

    /// <summary>Ringkasan tab Ringkasan: hitung mundur hari-H, progres task, budget, dan jumlah penugasan.</summary>
    [Authorize(Roles = Peran.AdminPic)]
    [HttpGet("{id:guid}/summary")]
    [ProducesResponseType<EventSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventSummaryDto>> GetSummary(Guid id, CancellationToken ct) =>
        Ok(await events.GetSummaryAsync(id, ct));

    /// <summary>Buat event. Status awal otomatis <c>draft</c>.</summary>
    [Authorize(Roles = Peran.AdminPic)]
    [HttpPost]
    [ProducesResponseType<EventDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EventDto>> Create(CreateEventDto dto, CancellationToken ct)
    {
        var hasil = await events.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = hasil.Id }, hasil);
    }

    /// <summary>Ubah data event. Status tidak bisa diubah lewat sini.</summary>
    [Authorize(Roles = Peran.AdminPic)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType<EventDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EventDto>> Update(Guid id, UpdateEventDto dto, CancellationToken ct) =>
        Ok(await events.UpdateAsync(id, dto, ct));

    /// <summary>Ubah status event. Transisi di luar alur yang sah ditolak 422.</summary>
    /// <remarks>
    /// draft → perencanaan/dibatalkan; perencanaan → dikonfirmasi/dibatalkan;
    /// dikonfirmasi → berlangsung/dibatalkan; berlangsung → selesai. Selesai dan dibatalkan adalah status akhir.
    /// </remarks>
    [Authorize(Roles = Peran.AdminPic)]
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType<EventDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EventDto>> UpdateStatus(Guid id, UpdateEventStatusDto dto, CancellationToken ct) =>
        Ok(await events.UpdateStatusAsync(id, dto, ct));

    /// <summary>Hapus permanen beserta seluruh isinya. Ditolak 422 jika status bukan draft atau dibatalkan.</summary>
    [Authorize(Roles = Peran.Admin)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await events.DeleteAsync(id, ct);
        return NoContent();
    }
}
