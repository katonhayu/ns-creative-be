using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Talents;
using NsCreative.Core.Services;
using NsCreative.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NsCreative.Api.Controllers;

/// <summary>Master data talent. Error ditangani ExceptionHandlingMiddleware.</summary>
[ApiController]
[Authorize(Roles = Peran.Semua)]
[Route("api/talents")]
[Produces("application/json")]
public class TalentsController(ITalentService talents) : ControllerBase
{
    /// <summary>Daftar talent berpaginasi. Pencarian pada nama, jenis, dan contact person.</summary>
    /// <remarks>sortBy: nama (default), jenis, feeStandar, createdAt. Filter <c>jenis</c> harus sama persis.</remarks>
    [Authorize(Roles = Peran.Pemantau)]
    [HttpGet]
    [ProducesResponseType<PagedResult<TalentListDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TalentListDto>>> GetList(
        [FromQuery] QueryParams query, [FromQuery] string? jenis, CancellationToken ct) =>
        Ok(await talents.GetListAsync(query, jenis, ct));

    /// <summary>Pilihan talent untuk dropdown penugasan (maksimal 50, urut nama).</summary>
    [HttpGet("lookup")]
    [ProducesResponseType<IReadOnlyList<TalentLookupDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TalentLookupDto>>> GetLookup([FromQuery] string? search, CancellationToken ct) =>
        Ok(await talents.GetLookupAsync(search, ct));

    [Authorize(Roles = Peran.Pemantau)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TalentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TalentDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await talents.GetByIdAsync(id, ct));

    /// <summary>Riwayat tampil talent beserta fee deal per event, terbaru lebih dulu.</summary>
    [Authorize(Roles = Peran.Pemantau)]
    [HttpGet("{id:guid}/events")]
    [ProducesResponseType<PagedResult<TalentEventDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<TalentEventDto>>> GetEvents(
        Guid id, [FromQuery] QueryParams query, CancellationToken ct) =>
        Ok(await talents.GetEventsAsync(id, query, ct));

    /// <summary>Event lain yang memakai talent ini pada tanggal yang beririsan. Array kosong berarti tidak bentrok.</summary>
    /// <remarks>Hanya peringatan. Event dibatalkan dan penugasan berstatus batal diabaikan.</remarks>
    [HttpGet("{id:guid}/conflicts")]
    [ProducesResponseType<IReadOnlyList<TalentBentrokDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TalentBentrokDto>>> GetConflicts(
        Guid id,
        [FromQuery] DateOnly? tanggalMulai,
        [FromQuery] DateOnly? tanggalSelesai,
        [FromQuery] Guid? kecualiEventId,
        CancellationToken ct) =>
        Ok(await talents.GetConflictsAsync(id, tanggalMulai, tanggalSelesai, kecualiEventId, ct));

    [Authorize(Roles = Peran.Admin)]
    [HttpPost]
    [ProducesResponseType<TalentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TalentDto>> Create(CreateTalentDto dto, CancellationToken ct)
    {
        var hasil = await talents.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = hasil.Id }, hasil);
    }

    [Authorize(Roles = Peran.Admin)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType<TalentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TalentDto>> Update(Guid id, UpdateTalentDto dto, CancellationToken ct) =>
        Ok(await talents.UpdateAsync(id, dto, ct));

    /// <summary>Soft delete. Penugasan di event lama tetap tersimpan.</summary>
    [Authorize(Roles = Peran.Admin)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await talents.DeleteAsync(id, ct);
        return NoContent();
    }
}
