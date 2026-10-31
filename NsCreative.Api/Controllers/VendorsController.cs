using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Vendors;
using NsCreative.Core.Services;
using NsCreative.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NsCreative.Api.Controllers;

/// <summary>Master data vendor. Error ditangani ExceptionHandlingMiddleware.</summary>
[ApiController]
[Authorize(Roles = Peran.Semua)]
[Route("api/vendors")]
[Produces("application/json")]
public class VendorsController(IVendorService vendors) : ControllerBase
{
    /// <summary>Daftar vendor berpaginasi. Pencarian pada nama, jenis jasa, dan contact person.</summary>
    /// <remarks>sortBy: nama (default), jenisJasa, createdAt. Filter <c>jenisJasa</c> harus sama persis.</remarks>
    [Authorize(Roles = Peran.Pemantau)]
    [HttpGet]
    [ProducesResponseType<PagedResult<VendorListDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<VendorListDto>>> GetList(
        [FromQuery] QueryParams query, [FromQuery] string? jenisJasa, CancellationToken ct) =>
        Ok(await vendors.GetListAsync(query, jenisJasa, ct));

    /// <summary>Pilihan vendor untuk dropdown penugasan (maksimal 50, urut nama).</summary>
    [HttpGet("lookup")]
    [ProducesResponseType<IReadOnlyList<VendorLookupDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VendorLookupDto>>> GetLookup(
        [FromQuery] string? search, [FromQuery] string? jenisJasa, CancellationToken ct) =>
        Ok(await vendors.GetLookupAsync(search, jenisJasa, ct));

    [Authorize(Roles = Peran.Pemantau)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType<VendorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VendorDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await vendors.GetByIdAsync(id, ct));

    /// <summary>
    /// Riwayat transaksi vendor per event (berpaginasi) beserta total seluruh transaksi.
    /// Event yang dibatalkan tidak ikut dihitung dalam total.
    /// </summary>
    [Authorize(Roles = Peran.Pemantau)]
    [HttpGet("{id:guid}/events")]
    [ProducesResponseType<VendorRiwayatDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VendorRiwayatDto>> GetEvents(
        Guid id, [FromQuery] QueryParams query, CancellationToken ct) =>
        Ok(await vendors.GetEventsAsync(id, query, ct));

    /// <summary>Event lain yang memesan vendor ini pada tanggal yang beririsan. Array kosong berarti tidak bentrok.</summary>
    /// <remarks>Hanya peringatan. Event dibatalkan diabaikan.</remarks>
    [HttpGet("{id:guid}/conflicts")]
    [ProducesResponseType<IReadOnlyList<VendorBentrokDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<VendorBentrokDto>>> GetConflicts(
        Guid id,
        [FromQuery] DateOnly? tanggalMulai,
        [FromQuery] DateOnly? tanggalSelesai,
        [FromQuery] Guid? kecualiEventId,
        CancellationToken ct) =>
        Ok(await vendors.GetConflictsAsync(id, tanggalMulai, tanggalSelesai, kecualiEventId, ct));

    [Authorize(Roles = Peran.Admin)]
    [HttpPost]
    [ProducesResponseType<VendorDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VendorDto>> Create(CreateVendorDto dto, CancellationToken ct)
    {
        var hasil = await vendors.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = hasil.Id }, hasil);
    }

    [Authorize(Roles = Peran.Admin)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType<VendorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VendorDto>> Update(Guid id, UpdateVendorDto dto, CancellationToken ct) =>
        Ok(await vendors.UpdateAsync(id, dto, ct));

    /// <summary>Soft delete. Transaksi di event lama tetap tersimpan.</summary>
    [Authorize(Roles = Peran.Admin)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await vendors.DeleteAsync(id, ct);
        return NoContent();
    }
}
