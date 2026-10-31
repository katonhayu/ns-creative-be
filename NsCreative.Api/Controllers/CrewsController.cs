using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Crews;
using NsCreative.Core.Services;
using NsCreative.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NsCreative.Api.Controllers;

/// <summary>Master data crew. Error ditangani ExceptionHandlingMiddleware.</summary>
[ApiController]
[Authorize(Roles = Peran.Semua)]
[Route("api/crews")]
[Produces("application/json")]
public class CrewsController(ICrewService crews) : ControllerBase
{
    /// <summary>Daftar crew berpaginasi. Pencarian pada nama, keahlian, dan telepon.</summary>
    /// <remarks>sortBy: nama (default), keahlian, createdAt. Filter <c>keahlian</c> berupa "mengandung".</remarks>
    [Authorize(Roles = Peran.Pemantau)]
    [HttpGet]
    [ProducesResponseType<PagedResult<CrewListDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CrewListDto>>> GetList(
        [FromQuery] QueryParams query, [FromQuery] string? keahlian, CancellationToken ct) =>
        Ok(await crews.GetListAsync(query, keahlian, ct));

    /// <summary>Pilihan crew untuk dropdown penugasan dan PIC event (maksimal 50, urut nama).</summary>
    /// <remarks><c>punyaAkun=true</c>: hanya crew dengan akun login aktif, yaitu yang bisa menjadi PIC event.</remarks>
    [HttpGet("lookup")]
    [ProducesResponseType<IReadOnlyList<CrewLookupDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CrewLookupDto>>> GetLookup(
        [FromQuery] string? search, [FromQuery] string? keahlian, [FromQuery] bool punyaAkun, CancellationToken ct) =>
        Ok(await crews.GetLookupAsync(search, keahlian, punyaAkun, ct));

    [Authorize(Roles = Peran.Pemantau)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType<CrewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CrewDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await crews.GetByIdAsync(id, ct));

    /// <summary>Riwayat penugasan crew, terbaru lebih dulu.</summary>
    [Authorize(Roles = Peran.Pemantau)]
    [HttpGet("{id:guid}/events")]
    [ProducesResponseType<PagedResult<CrewEventDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<CrewEventDto>>> GetEvents(
        Guid id, [FromQuery] QueryParams query, CancellationToken ct) =>
        Ok(await crews.GetEventsAsync(id, query, ct));

    /// <summary>Event lain yang menugaskan crew ini pada tanggal yang beririsan. Array kosong berarti tidak bentrok.</summary>
    /// <remarks>
    /// Hanya peringatan, tidak memblokir penugasan. <c>tanggalSelesai</c> opsional (default sama dengan tanggalMulai).
    /// Isi <c>kecualiEventId</c> dengan event yang sedang diedit supaya penugasannya sendiri tidak ikut terhitung.
    /// Event yang dibatalkan diabaikan.
    /// </remarks>
    [HttpGet("{id:guid}/conflicts")]
    [ProducesResponseType<IReadOnlyList<CrewBentrokDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CrewBentrokDto>>> GetConflicts(
        Guid id,
        [FromQuery] DateOnly? tanggalMulai,
        [FromQuery] DateOnly? tanggalSelesai,
        [FromQuery] Guid? kecualiEventId,
        CancellationToken ct) =>
        Ok(await crews.GetConflictsAsync(id, tanggalMulai, tanggalSelesai, kecualiEventId, ct));

    /// <summary>Tambah crew. <c>userId</c> opsional: akun berperan crew yang belum ditautkan.</summary>
    [Authorize(Roles = Peran.Admin)]
    [HttpPost]
    [ProducesResponseType<CrewDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CrewDto>> Create(CreateCrewDto dto, CancellationToken ct)
    {
        var hasil = await crews.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = hasil.Id }, hasil);
    }

    [Authorize(Roles = Peran.Admin)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType<CrewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CrewDto>> Update(Guid id, UpdateCrewDto dto, CancellationToken ct) =>
        Ok(await crews.UpdateAsync(id, dto, ct));

    /// <summary>Soft delete. Penugasan di event lama tetap tersimpan.</summary>
    [Authorize(Roles = Peran.Admin)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await crews.DeleteAsync(id, ct);
        return NoContent();
    }
}
