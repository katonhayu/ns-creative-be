using NsCreative.Core.Common;
using NsCreative.Core.Dtos.EventVendors;
using NsCreative.Core.Services;
using NsCreative.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NsCreative.Api.Controllers;

/// <summary>
/// Transaksi vendor di event. Endpoint bertanda ⚡ mengubah budget dalam transaksi yang sama.
/// Semua perubahan ditolak 422 jika event sudah selesai atau dibatalkan.
/// </summary>
[ApiController]
[Authorize(Roles = Peran.Semua)]
[Route("api/events/{eventId:guid}/vendors")]
[Produces("application/json")]
public class EventVendorsController(IEventVendorService vendors) : ControllerBase
{
    /// <summary>Daftar transaksi vendor beserta realisasi budgetnya.</summary>
    /// <remarks>sortBy: nama (default), jenisJasa, biaya, statusPembayaran, createdAt. Pencarian pada nama vendor, jenis jasa, dan kebutuhan.</remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<EventVendorDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<EventVendorDto>>> GetList(
        Guid eventId, [FromQuery] QueryParams query, CancellationToken ct) =>
        Ok(await vendors.GetListAsync(eventId, query, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<EventVendorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventVendorDto>> GetById(Guid eventId, Guid id, CancellationToken ct) =>
        Ok(await vendors.GetByIdAsync(eventId, id, ct));

    /// <summary>⚡ Tambah transaksi vendor. Baris budget kategori Vendor (estimasi = biaya) dibuat otomatis.</summary>
    /// <remarks>Status pembayaran opsional, default <c>belum_bayar</c>, dan disalin ke baris budget.</remarks>
    [Authorize(Roles = Peran.Pengelola)]
    [HttpPost]
    [ProducesResponseType<EventVendorDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EventVendorDto>> Create(Guid eventId, CreateEventVendorDto dto, CancellationToken ct)
    {
        var hasil = await vendors.CreateAsync(eventId, dto, ct);
        return CreatedAtAction(nameof(GetById), new { eventId, id = hasil.Id }, hasil);
    }

    /// <summary>⚡ Ubah kebutuhan dan biaya. Estimasi budget ikut biaya; biaya aktual tidak diubah.</summary>
    [Authorize(Roles = Peran.Pengelola)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType<EventVendorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EventVendorDto>> Update(
        Guid eventId, Guid id, UpdateEventVendorDto dto, CancellationToken ct) =>
        Ok(await vendors.UpdateAsync(eventId, id, dto, ct));

    /// <summary>⚡ Ubah status pembayaran di transaksi vendor dan budget terkait sekaligus.</summary>
    [Authorize(Roles = Peran.Pengelola)]
    [HttpPatch("{id:guid}/pembayaran")]
    [ProducesResponseType<EventVendorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EventVendorDto>> UpdatePembayaran(
        Guid eventId, Guid id, UpdateEventVendorPembayaranDto dto, CancellationToken ct) =>
        Ok(await vendors.UpdatePembayaranAsync(eventId, id, dto, ct));

    /// <summary>⚡ Hapus transaksi beserta budget terkait. Ditolak 422 jika biaya aktual sudah dicatat.</summary>
    [Authorize(Roles = Peran.Pengelola)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid eventId, Guid id, CancellationToken ct)
    {
        await vendors.DeleteAsync(eventId, id, ct);
        return NoContent();
    }
}
