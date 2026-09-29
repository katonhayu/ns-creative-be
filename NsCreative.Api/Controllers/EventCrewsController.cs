using NsCreative.Core.Common;
using NsCreative.Core.Dtos.EventCrews;
using NsCreative.Core.Services;
using NsCreative.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NsCreative.Api.Controllers;

/// <summary>
/// Penugasan crew ke event. Tidak ada budget otomatis. Setiap baris membawa daftar
/// <c>bentrok</c> (peringatan, tidak memblokir). Perubahan ditolak 422 jika event sudah selesai atau dibatalkan.
/// </summary>
[ApiController]
[Authorize]
[Route("api/events/{eventId:guid}/crews")]
[Produces("application/json")]
public class EventCrewsController(IEventCrewService crews) : ControllerBase
{
    /// <summary>Daftar crew yang ditugaskan beserta peringatan bentrok jadwal.</summary>
    /// <remarks>sortBy: tanggalBertugas (default, kosong di akhir), nama, posisi, createdAt. Pencarian pada nama, keahlian, dan posisi.</remarks>
    [Authorize(Roles = Peran.Semua)]
    [HttpGet]
    [ProducesResponseType<PagedResult<EventCrewDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<EventCrewDto>>> GetList(
        Guid eventId, [FromQuery] QueryParams query, CancellationToken ct) =>
        Ok(await crews.GetListAsync(eventId, query, ct));

    [Authorize(Roles = Peran.Semua)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType<EventCrewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventCrewDto>> GetById(Guid eventId, Guid id, CancellationToken ct) =>
        Ok(await crews.GetByIdAsync(eventId, id, ct));

    /// <summary>Tugaskan crew. Jika bentrok dengan event lain, tetap tersimpan dan <c>bentrok</c> berisi daftarnya.</summary>
    [Authorize(Roles = Peran.AdminPic)]
    [HttpPost]
    [ProducesResponseType<EventCrewDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EventCrewDto>> Create(Guid eventId, CreateEventCrewDto dto, CancellationToken ct)
    {
        var hasil = await crews.CreateAsync(eventId, dto, ct);
        return CreatedAtAction(nameof(GetById), new { eventId, id = hasil.Id }, hasil);
    }

    /// <summary>Ubah posisi dan tanggal bertugas.</summary>
    [Authorize(Roles = Peran.AdminPic)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType<EventCrewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EventCrewDto>> Update(
        Guid eventId, Guid id, UpdateEventCrewDto dto, CancellationToken ct) =>
        Ok(await crews.UpdateAsync(eventId, id, dto, ct));

    [Authorize(Roles = Peran.AdminPic)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid eventId, Guid id, CancellationToken ct)
    {
        await crews.DeleteAsync(eventId, id, ct);
        return NoContent();
    }
}
