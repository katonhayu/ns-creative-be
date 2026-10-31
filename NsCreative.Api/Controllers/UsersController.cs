using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Users;
using NsCreative.Core.Enums;
using NsCreative.Core.Services;
using NsCreative.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NsCreative.Api.Controllers;

/// <summary>Akun pengguna (khusus admin, kecuali lookup untuk admin dan pic). Error ditangani ExceptionHandlingMiddleware.</summary>
[ApiController]
[Authorize(Roles = Peran.Pemantau)]
[Route("api/users")]
[Produces("application/json")]
public class UsersController(IUserService users) : ControllerBase
{
    /// <summary>Daftar pengguna berpaginasi. Pencarian pada nama dan email.</summary>
    /// <remarks>sortBy: nama (default), email, role, createdAt. Filter: role (admin/pic/crew), isAktif.</remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<UserListDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<UserListDto>>> GetList(
        [FromQuery] QueryParams query, [FromQuery] UserRole? role, [FromQuery] bool? isAktif, CancellationToken ct) =>
        Ok(await users.GetListAsync(query, role, isAktif, ct));

    /// <summary>Pilihan pengguna aktif untuk dropdown, misalnya PIC event (maksimal 50).</summary>
    /// <remarks><c>role</c> boleh diulang: <c>?role=admin&amp;role=pic</c>.</remarks>
    [HttpGet("lookup")]
    [ProducesResponseType<IReadOnlyList<UserLookupDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserLookupDto>>> GetLookup(
        [FromQuery] string? search, [FromQuery] UserRole[]? role, CancellationToken ct) =>
        Ok(await users.GetLookupAsync(search, role, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await users.GetByIdAsync(id, ct));

    /// <summary>Buat pengguna. Kata sandi disimpan sebagai hash.</summary>
    [Authorize(Roles = Peran.Admin)]
    [HttpPost]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserDto>> Create(CreateUserDto dto, CancellationToken ct)
    {
        var hasil = await users.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = hasil.Id }, hasil);
    }

    /// <summary>Ubah nama, email, dan peran.</summary>
    [Authorize(Roles = Peran.Admin)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserDto>> Update(Guid id, UpdateUserDto dto, CancellationToken ct) =>
        Ok(await users.UpdateAsync(id, dto, ct));

    /// <summary>Aktifkan atau nonaktifkan akun. Akun nonaktif tidak bisa login.</summary>
    [Authorize(Roles = Peran.Admin)]
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> UpdateStatus(Guid id, UpdateUserStatusDto dto, CancellationToken ct) =>
        Ok(await users.UpdateStatusAsync(id, dto, ct));

    /// <summary>Atur kata sandi baru untuk pengguna.</summary>
    [Authorize(Roles = Peran.Admin)]
    [HttpPost("{id:guid}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordDto dto, CancellationToken ct)
    {
        await users.ResetPasswordAsync(id, dto, ct);
        return NoContent();
    }

    /// <summary>Hapus permanen. Ditolak (422) jika pengguna tercatat sebagai PIC event.</summary>
    [Authorize(Roles = Peran.Admin)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await users.DeleteAsync(id, ct);
        return NoContent();
    }
}
