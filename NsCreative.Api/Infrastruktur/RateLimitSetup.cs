using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;
using NsCreative.Api.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace NsCreative.Api.Infrastruktur;

/// <summary>Bagian "RateLimit" di appsettings.json. Semua angka per menit.</summary>
public class RateLimitOptions
{
    public int LoginPerMenit { get; set; } = 10;
    public int RefreshPerMenit { get; set; } = 30;
    public int RequestPerMenit { get; set; } = 300;
}

/// <summary>
/// Rate limiting bawaan ASP.NET Core. Login dan refresh dibatasi per IP (menahan tebak kata sandi),
/// request lain per pengguna login (atau per IP jika belum login). Melebihi batas → 429.
/// </summary>
public static class RateLimitSetup
{
    public const string Login = "login";
    public const string Refresh = "refresh";

    private static readonly JsonSerializerOptions JsonOpsi = new(JsonSerializerDefaults.Web);

    public static IServiceCollection AddRateLimitNsCreative(this IServiceCollection services, IConfiguration config)
    {
        var opsi = config.GetSection("RateLimit").Get<RateLimitOptions>() ?? new RateLimitOptions();

        services.AddRateLimiter(o =>
        {
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                RateLimitPartition.GetSlidingWindowLimiter(KunciPengguna(ctx), _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = opsi.RequestPerMenit,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0,
                }));

            o.AddPolicy(Login, ctx => PerIp(ctx, opsi.LoginPerMenit));
            o.AddPolicy(Refresh, ctx => PerIp(ctx, opsi.RefreshPerMenit));

            o.OnRejected = async (ctx, ct) =>
            {
                var detik = ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var jeda)
                    ? (int)Math.Ceiling(jeda.TotalSeconds)
                    : 60;
                var http = ctx.HttpContext;
                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                http.Response.Headers.RetryAfter = detik.ToString(CultureInfo.InvariantCulture);
                http.Response.ContentType = "application/problem+json";

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Terlalu banyak permintaan.",
                    Detail = $"Coba lagi dalam {detik} detik.",
                    Instance = http.Request.Path,
                };
                problem.Extensions["traceId"] = http.TraceIdentifier;
                await http.Response.WriteAsJsonAsync(problem, JsonOpsi, ct);
            };
        });
        return services;
    }

    private static RateLimitPartition<string> PerIp(HttpContext ctx, int batas) =>
        RateLimitPartition.GetFixedWindowLimiter(Ip(ctx), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = batas,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });

    private static string KunciPengguna(HttpContext ctx) =>
        ctx.User.FindFirst(KlaimJwt.Sub)?.Value is { } sub ? $"user:{sub}" : $"ip:{Ip(ctx)}";

    private static string Ip(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "tidak-diketahui";
}
