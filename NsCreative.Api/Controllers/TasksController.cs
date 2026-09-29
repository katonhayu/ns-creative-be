using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Tasks;
using NsCreative.Core.Enums;
using NsCreative.Core.Services;
using NsCreative.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NsCreative.Api.Controllers;

/// <summary>
/// Task persiapan event. Perubahan ditolak 422 jika event sudah selesai atau dibatalkan.
/// Crew hanya melihat dan mengubah status task yang PIC-nya dirinya.
/// </summary>
[ApiController]
[Authorize]
[Route("api/events/{eventId:guid}/tasks")]
[Produces("application/json")]
public class TasksController(ITaskService tasks) : ControllerBase
{
    /// <summary>Daftar task berpaginasi. Pencarian pada nama tugas, deskripsi, dan nama PIC.</summary>
    /// <remarks>
    /// sortBy: deadline (default, kosong di akhir), namaTugas, status, createdAt.
    /// Filter status: belum, dikerjakan, selesai (status tersimpan), atau terlambat (deadline lewat dan belum selesai).
    /// </remarks>
    [Authorize(Roles = Peran.Semua)]
    [HttpGet]
    [ProducesResponseType<PagedResult<TaskDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<TaskDto>>> GetList(
        Guid eventId, [FromQuery] QueryParams query, [FromQuery] TaskFilter filter, CancellationToken ct) =>
        Ok(await tasks.GetListAsync(eventId, query, filter, ct));

    [Authorize(Roles = Peran.Semua)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TaskDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskDto>> GetById(Guid eventId, Guid id, CancellationToken ct) =>
        Ok(await tasks.GetByIdAsync(eventId, id, ct));

    /// <summary>Task lintas event yang terlihat oleh pengguna (maksimal 500, urut deadline). Tidak berpaginasi.</summary>
    /// <remarks>Crew selalu hanya melihat task miliknya. <c>milikSaya=true</c> untuk admin/pic.</remarks>
    [Authorize(Roles = Peran.Semua)]
    [HttpGet("~/api/tasks")]
    [ProducesResponseType<IReadOnlyList<TaskDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TaskDto>>> GetLintasEvent(
        [FromQuery] TaskLintasEventFilter filter, CancellationToken ct) =>
        Ok(await tasks.GetLintasEventAsync(filter, ct));

    /// <summary>Task milik pengguna login di semua event (kecuali event dibatalkan), urut deadline.</summary>
    /// <remarks>Filter status sama dengan daftar task event, termasuk <c>terlambat</c>.</remarks>
    [Authorize(Roles = Peran.Semua)]
    [HttpGet("~/api/tasks/mine")]
    [ProducesResponseType<PagedResult<MyTaskDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<MyTaskDto>>> GetMine(
        [FromQuery] QueryParams query, [FromQuery] TaskItemStatus? status, CancellationToken ct) =>
        Ok(await tasks.GetMineAsync(query, status, ct));

    /// <summary>Buat task. Status awal otomatis <c>belum</c>.</summary>
    [Authorize(Roles = Peran.AdminPic)]
    [HttpPost]
    [ProducesResponseType<TaskDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TaskDto>> Create(Guid eventId, CreateTaskDto dto, CancellationToken ct)
    {
        var hasil = await tasks.CreateAsync(eventId, dto, ct);
        return CreatedAtAction(nameof(GetById), new { eventId, id = hasil.Id }, hasil);
    }

    /// <summary>Ubah isi task. Status tidak bisa diubah lewat sini.</summary>
    [Authorize(Roles = Peran.AdminPic)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType<TaskDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TaskDto>> Update(Guid eventId, Guid id, UpdateTaskDto dto, CancellationToken ct) =>
        Ok(await tasks.UpdateAsync(eventId, id, dto, ct));

    /// <summary>Ubah status: belum, dikerjakan, atau selesai. <c>terlambat</c> ditolak karena dihitung otomatis.</summary>
    [Authorize(Roles = Peran.Semua)]
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType<TaskDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TaskDto>> UpdateStatus(
        Guid eventId, Guid id, UpdateTaskStatusDto dto, CancellationToken ct) =>
        Ok(await tasks.UpdateStatusAsync(eventId, id, dto, ct));

    [Authorize(Roles = Peran.AdminPic)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid eventId, Guid id, CancellationToken ct)
    {
        await tasks.DeleteAsync(eventId, id, ct);
        return NoContent();
    }
}
