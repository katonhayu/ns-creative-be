using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Clients;
using NsCreative.Core.Services;
using NsCreative.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NsCreative.Api.Controllers;

/// <summary>Master data client. Error ditangani ExceptionHandlingMiddleware.</summary>
[ApiController]
[Authorize(Roles = Peran.Semua)]
[Route("api/clients")]
[Produces("application/json")]
public class ClientsController(IClientService clients) : ControllerBase
{
    /// <summary>Daftar client berpaginasi. Pencarian pada nama dan perusahaan.</summary>
    /// <remarks>sortBy: nama (default), perusahaan, createdAt. sortDir: asc (default), desc.</remarks>
    [Authorize(Roles = Peran.Pemantau)]
    [HttpGet]
    [ProducesResponseType<PagedResult<ClientListDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ClientListDto>>> GetList([FromQuery] QueryParams query, CancellationToken ct) =>
        Ok(await clients.GetListAsync(query, ct));

    /// <summary>Pilihan client untuk dropdown (maksimal 50, urut nama).</summary>
    [HttpGet("lookup")]
    [ProducesResponseType<IReadOnlyList<ClientLookupDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClientLookupDto>>> GetLookup([FromQuery] string? search, CancellationToken ct) =>
        Ok(await clients.GetLookupAsync(search, ct));

    [Authorize(Roles = Peran.Pemantau)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ClientDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await clients.GetByIdAsync(id, ct));

    /// <summary>Riwayat event client, terbaru lebih dulu. Tetap bisa dibuka walau client sudah dihapus.</summary>
    [Authorize(Roles = Peran.Pemantau)]
    [HttpGet("{id:guid}/events")]
    [ProducesResponseType<PagedResult<ClientEventDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<ClientEventDto>>> GetEvents(
        Guid id, [FromQuery] QueryParams query, CancellationToken ct) =>
        Ok(await clients.GetEventsAsync(id, query, ct));

    [Authorize(Roles = Peran.Admin)]
    [HttpPost]
    [ProducesResponseType<ClientDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClientDto>> Create(CreateClientDto dto, CancellationToken ct)
    {
        var hasil = await clients.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = hasil.Id }, hasil);
    }

    [Authorize(Roles = Peran.Admin)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType<ClientDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientDto>> Update(Guid id, UpdateClientDto dto, CancellationToken ct) =>
        Ok(await clients.UpdateAsync(id, dto, ct));

    /// <summary>Soft delete. Ditolak (422) jika client masih punya event aktif.</summary>
    [Authorize(Roles = Peran.Admin)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await clients.DeleteAsync(id, ct);
        return NoContent();
    }
}
