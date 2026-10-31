using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Budgets;
using NsCreative.Core.Services;
using NsCreative.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NsCreative.Api.Controllers;

/// <summary>
/// Anggaran event. Pos dari talent/vendor (<c>isTerkunci</c>) hanya bisa dicatat realisasinya.
/// Semua perubahan ditolak 422 jika event sudah selesai atau dibatalkan.
/// </summary>
[ApiController]
[Authorize(Roles = Peran.Semua)]
[Route("api/events/{eventId:guid}/budgets")]
[Produces("application/json")]
public class BudgetsController(IBudgetService budgets) : ControllerBase
{
    /// <summary>Daftar pos budget. Setiap baris membawa <c>selisih</c> dan <c>isTerkunci</c>.</summary>
    /// <remarks>
    /// sortBy: kategori (default), namaKebutuhan, estimasiBiaya, biayaAktual, selisih, statusPembayaran, createdAt.
    /// Filter: kategori (sama persis), statusPembayaran (belum_bayar, dp, lunas). Pencarian pada nama kebutuhan dan kategori.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<BudgetDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<BudgetDto>>> GetList(
        Guid eventId, [FromQuery] QueryParams query, [FromQuery] BudgetFilter filter, CancellationToken ct) =>
        Ok(await budgets.GetListAsync(eventId, query, filter, ct));

    /// <summary>Total per kategori dan grand total. <c>netPengeluaran = aktual - sponsorship</c>.</summary>
    [HttpGet("summary")]
    [ProducesResponseType<BudgetSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BudgetSummaryDto>> GetSummary(Guid eventId, CancellationToken ct) =>
        Ok(await budgets.GetSummaryAsync(eventId, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<BudgetDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BudgetDto>> GetById(Guid eventId, Guid id, CancellationToken ct) =>
        Ok(await budgets.GetByIdAsync(eventId, id, ct));

    /// <summary>Tambah pos manual. Pos talent/vendor dibuat otomatis lewat penugasan, bukan di sini.</summary>
    [Authorize(Roles = Peran.Pengelola)]
    [HttpPost]
    [ProducesResponseType<BudgetDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BudgetDto>> Create(Guid eventId, CreateBudgetDto dto, CancellationToken ct)
    {
        var hasil = await budgets.CreateAsync(eventId, dto, ct);
        return CreatedAtAction(nameof(GetById), new { eventId, id = hasil.Id }, hasil);
    }

    /// <summary>Ubah pos manual. Ditolak 422 untuk pos terkunci.</summary>
    [Authorize(Roles = Peran.Pengelola)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType<BudgetDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BudgetDto>> Update(Guid eventId, Guid id, UpdateBudgetDto dto, CancellationToken ct) =>
        Ok(await budgets.UpdateAsync(eventId, id, dto, ct));

    /// <summary>⚡ Catat realisasi (biaya aktual, status pembayaran, sponsorship opsional). Boleh untuk semua pos.</summary>
    /// <remarks>Untuk pos dari vendor, status pembayaran di transaksi vendor ikut diubah.</remarks>
    [Authorize(Roles = Peran.Pengelola)]
    [HttpPatch("{id:guid}/realisasi")]
    [ProducesResponseType<BudgetDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BudgetDto>> UpdateRealisasi(
        Guid eventId, Guid id, UpdateRealisasiDto dto, CancellationToken ct) =>
        Ok(await budgets.UpdateRealisasiAsync(eventId, id, dto, ct));

    /// <summary>Hapus pos manual. Ditolak 422 untuk pos terkunci.</summary>
    [Authorize(Roles = Peran.Pengelola)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid eventId, Guid id, CancellationToken ct)
    {
        await budgets.DeleteAsync(eventId, id, ct);
        return NoContent();
    }
}
