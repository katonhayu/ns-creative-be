using System.Text;
using System.Text.Json;
using NsCreative.Core.Enums;
using NsCreative.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace NsCreative.Api.Auth;

public static class AuthSetup
{
    private const string PesanAkunBerubah = "Akun nonaktif atau perannya berubah. Silakan login ulang.";

    /// <summary>
    /// JWT Bearer + otorisasi. Semua endpoint wajib login (fallback policy) kecuali yang diberi
    /// [AllowAnonymous]. Setiap request juga memastikan akun masih aktif dan perannya belum
    /// berubah, supaya menonaktifkan akun atau mengganti peran langsung berlaku tanpa menunggu
    /// access token kedaluwarsa.
    /// </summary>
    public static IServiceCollection AddAuthNsCreative(this IServiceCollection services, JwtOptions opsi)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = opsi.Issuer,
                    ValidAudience = opsi.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opsi.Key)),
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = KlaimJwt.Nama,
                    RoleClaimType = KlaimJwt.Role,
                };
                o.Events = new JwtBearerEvents
                {
                    OnTokenValidated = CekAkunMasihBerlakuAsync,
                    OnChallenge = async ctx =>
                    {
                        ctx.HandleResponse();
                        var detail = ctx.AuthenticateFailure switch
                        {
                            SecurityTokenExpiredException => "Sesi login sudah berakhir. Perbarui token lewat /api/auth/refresh.",
                            { Message: PesanAkunBerubah } => PesanAkunBerubah,
                            not null => "Token login tidak valid. Silakan login ulang.",
                            null => "Silakan login terlebih dahulu.",
                        };
                        ctx.Response.Headers.WWWAuthenticate = "Bearer";
                        await TulisProblemAsync(ctx.HttpContext, StatusCodes.Status401Unauthorized, "Belum login.", detail);
                    },
                    OnForbidden = ctx => TulisProblemAsync(ctx.HttpContext, StatusCodes.Status403Forbidden,
                        "Akses ditolak.", "Peran Anda tidak memiliki akses ke fitur ini."),
                };
            });

        services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build());
        return services;
    }

    private static async Task CekAkunMasihBerlakuAsync(TokenValidatedContext ctx)
    {
        var principal = ctx.Principal!;
        if (!Guid.TryParse(principal.FindFirst(KlaimJwt.Sub)?.Value, out var id))
        {
            ctx.Fail(PesanAkunBerubah);
            return;
        }

        var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var akun = await db.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new { u.IsAktif, u.Role })
            .FirstOrDefaultAsync(ctx.HttpContext.RequestAborted);
        if (akun is null || !akun.IsAktif || akun.Role.NamaKlaim() != principal.FindFirst(KlaimJwt.Role)?.Value)
            ctx.Fail(PesanAkunBerubah);
    }

    private static readonly JsonSerializerOptions JsonOpsi = new(JsonSerializerDefaults.Web);

    private static Task TulisProblemAsync(HttpContext http, int status, string title, string detail)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail, Instance = http.Request.Path };
        problem.Extensions["traceId"] = http.TraceIdentifier;
        http.Response.StatusCode = status;
        http.Response.ContentType = "application/problem+json";
        return http.Response.WriteAsJsonAsync(problem, JsonOpsi);
    }
}
