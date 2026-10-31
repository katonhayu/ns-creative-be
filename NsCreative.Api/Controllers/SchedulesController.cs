using NsCreative.Core.Dtos.Schedules;
using NsCreative.Core.Services;
using NsCreative.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NsCreative.Api.Controllers;

/// <summary>
/// Rundown event. Perubahan ditolak 422 jika event sudah selesai atau dibatalkan.
/// Tanggal di luar rentang event tetap tersimpan dan ditandai <c>diLuarRentangEvent</c>.
/// </summary>
[ApiController]
[Authorize]
[Route("api/events/{eventId:guid}/schedules")]
[Produces("application/json")]
public class SchedulesController(IScheduleService schedules) : ControllerBase
{
    /// <summary>Rundown dikelompokkan per tanggal, tiap hari diurutkan waktu mulai. Tidak berpaginasi.</summary>
    [Authorize(Roles = Peran.Semua)]
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RundownHariDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RundownHariDto>>> GetList(
        Guid eventId, [FromQuery] DateOnly? tanggal, CancellationToken ct) =>
        Ok(await schedules.GetListAsync(eventId, tanggal, ct));

    /// <summary>Jadwal lintas event yang terlihat oleh pengguna, urut tanggal dan jam. Tidak berpaginasi.</summary>
    /// <remarks>Filter opsional: <c>eventId</c>, <c>dariTanggal</c>, <c>sampaiTanggal</c>. Maksimal 500 sesi dengan tanggal terbaru.</remarks>
    [Authorize(Roles = Peran.Semua)]
    [HttpGet("~/api/schedules")]
    [ProducesResponseType<IReadOnlyList<ScheduleDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ScheduleDto>>> GetLintasEvent(
        [FromQuery] JadwalFilter filter, CancellationToken ct) =>
        Ok(await schedules.GetLintasEventAsync(filter, ct));

    [Authorize(Roles = Peran.Semua)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ScheduleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScheduleDto>> GetById(Guid eventId, Guid id, CancellationToken ct) =>
        Ok(await schedules.GetByIdAsync(eventId, id, ct));

    [Authorize(Roles = Peran.Pengelola)]
    [HttpPost]
    [ProducesResponseType<ScheduleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ScheduleDto>> Create(Guid eventId, CreateScheduleDto dto, CancellationToken ct)
    {
        var hasil = await schedules.CreateAsync(eventId, dto, ct);
        return CreatedAtAction(nameof(GetById), new { eventId, id = hasil.Id }, hasil);
    }

    /// <summary>⚡ Input rundown sekaligus (maksimal 200 sesi). Satu baris gagal validasi, tidak ada yang tersimpan.</summary>
    /// <remarks>Error per baris memakai kunci berindeks, misalnya <c>[2].waktuSelesai</c>.</remarks>
    [Authorize(Roles = Peran.Pengelola)]
    [HttpPost("bulk")]
    [ProducesResponseType<IReadOnlyList<ScheduleDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyList<ScheduleDto>>> CreateBulk(
        Guid eventId, IReadOnlyList<CreateScheduleDto> daftar, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await schedules.CreateBulkAsync(eventId, daftar, ct));

    [Authorize(Roles = Peran.Pengelola)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType<ScheduleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ScheduleDto>> Update(Guid eventId, Guid id, UpdateScheduleDto dto, CancellationToken ct) =>
        Ok(await schedules.UpdateAsync(eventId, id, dto, ct));

    [Authorize(Roles = Peran.Pengelola)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid eventId, Guid id, CancellationToken ct)
    {
        await schedules.DeleteAsync(eventId, id, ct);
        return NoContent();
    }
}
