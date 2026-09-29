using NsCreative.Core.Dtos.Auth;
using NsCreative.Core.Services;
using NsCreative.Api.Infrastruktur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace NsCreative.Api.Controllers;

/// <summary>
/// Login dan sesi. Access token berlaku 15 menit; perbarui lewat <c>/refresh</c> dengan refresh token
/// (7 hari). Setiap refresh memberi refresh token baru dan mencabut yang lama.
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(IAuthService auth) : ControllerBase
{
    /// <summary>Login dengan email dan kata sandi.</summary>
    /// <remarks>Dibatasi per IP (default 10 kali per menit); lebih dari itu 429 dengan header Retry-After.</remarks>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitSetup.Login)]
    [HttpPost("login")]
    [ProducesResponseType<LoginResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginDto dto, CancellationToken ct) =>
        Ok(await auth.LoginAsync(dto, ct));

    /// <summary>Tukar refresh token dengan pasangan token baru.</summary>
    /// <remarks>
    /// Refresh token yang sudah pernah dipakai tidak bisa dipakai lagi. Jika dipakai ulang, semua sesi akun
    /// dicabut (dianggap bocor). Frontend sebaiknya memastikan hanya satu permintaan refresh berjalan sekaligus.
    /// </remarks>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitSetup.Refresh)]
    [HttpPost("refresh")]
    [ProducesResponseType<LoginResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<LoginResponseDto>> Refresh(RefreshTokenDto dto, CancellationToken ct) =>
        Ok(await auth.RefreshAsync(dto, ct));

    /// <summary>Cabut refresh token sesi ini. Access token yang sudah terbit tetap berlaku sampai kedaluwarsa.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(RefreshTokenDto dto, CancellationToken ct)
    {
        await auth.LogoutAsync(dto, ct);
        return NoContent();
    }

    /// <summary>Profil pengguna login dan perannya.</summary>
    [HttpGet("me")]
    [ProducesResponseType<AuthUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthUserDto>> Me(CancellationToken ct) =>
        Ok(await auth.GetMeAsync(ct));

    /// <summary>Ganti kata sandi sendiri. Sesi lain dicabut; respons berisi token baru untuk sesi ini.</summary>
    [HttpPost("change-password")]
    [ProducesResponseType<LoginResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponseDto>> ChangePassword(ChangePasswordDto dto, CancellationToken ct) =>
        Ok(await auth.ChangePasswordAsync(dto, ct));
}
