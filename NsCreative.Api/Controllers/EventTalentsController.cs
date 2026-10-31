using NsCreative.Core.Common;
using NsCreative.Core.Dtos.EventTalents;
using NsCreative.Core.Services;
using NsCreative.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NsCreative.Api.Controllers;

/// <summary>
/// Penugasan talent ke event. Endpoint bertanda ⚡ mengubah budget dalam transaksi yang sama.
/// Semua perubahan ditolak 422 jika event sudah selesai atau dibatalkan.
/// </summary>
[ApiController]
[Authorize(Roles = Peran.Semua)]
[Route("api/events/{eventId:guid}/talents")]
[Produces("application/json")]
public class EventTalentsController(IEventTalentService talents) : ControllerBase
{
    /// <summary>Daftar talent yang ditugaskan beserta fee deal dan realisasi budgetnya.</summary>
    /// <remarks>sortBy: jadwalTampil (default, kosong di akhir), nama, feeDeal, status, createdAt. Pencarian pada nama dan jenis talent.</remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<EventTalentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<EventTalentDto>>> GetList(
        Guid eventId, [FromQuery] QueryParams query, CancellationToken ct) =>
        Ok(await talents.GetListAsync(eventId, query, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<EventTalentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventTalentDto>> GetById(Guid eventId, Guid id, CancellationToken ct) =>
        Ok(await talents.GetByIdAsync(eventId, id, ct));

    /// <summary>⚡ Tugaskan talent. Baris budget kategori Talent (estimasi = fee deal) dibuat otomatis.</summary>
    /// <remarks><c>jadwalTampil</c> tanpa zona waktu dianggap WIB. Status opsional, default <c>dihubungi</c>.</remarks>
    [Authorize(Roles = Peran.Pengelola)]
    [HttpPost]
    [ProducesResponseType<EventTalentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EventTalentDto>> Create(Guid eventId, CreateEventTalentDto dto, CancellationToken ct)
    {
        var hasil = await talents.CreateAsync(eventId, dto, ct);
        return CreatedAtAction(nameof(GetById), new { eventId, id = hasil.Id }, hasil);
    }

    /// <summary>⚡ Ubah detail penugasan. Estimasi budget ikut fee deal; biaya aktual tidak diubah.</summary>
    [Authorize(Roles = Peran.Pengelola)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType<EventTalentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EventTalentDto>> Update(
        Guid eventId, Guid id, UpdateEventTalentDto dto, CancellationToken ct) =>
        Ok(await talents.UpdateAsync(eventId, id, dto, ct));

    /// <summary>Ubah status penugasan: dihubungi, deal, kontrak, selesai, batal.</summary>
    [Authorize(Roles = Peran.Pengelola)]
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType<EventTalentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EventTalentDto>> UpdateStatus(
        Guid eventId, Guid id, UpdateEventTalentStatusDto dto, CancellationToken ct) =>
        Ok(await talents.UpdateStatusAsync(eventId, id, dto, ct));

    /// <summary>⚡ Hapus penugasan beserta budget terkait. Ditolak 422 jika biaya aktual sudah dicatat.</summary>
    [Authorize(Roles = Peran.Pengelola)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid eventId, Guid id, CancellationToken ct)
    {
        await talents.DeleteAsync(eventId, id, ct);
        return NoContent();
    }
}
