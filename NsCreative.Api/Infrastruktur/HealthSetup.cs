using System.Text.Json;
using NsCreative.Data;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace NsCreative.Api.Infrastruktur;

/// <summary>
/// <c>/health/live</c>: proses berjalan (tanpa cek apa pun, untuk restart otomatis).
/// <c>/health/ready</c>: database bisa dihubungi dan semua migration sudah diterapkan (untuk load balancer).
/// Keduanya tanpa login dan tanpa rate limit.
/// </summary>
public static class HealthSetup
{
    private const string TagSiap = "ready";
    private static readonly JsonSerializerOptions JsonOpsi = new(JsonSerializerDefaults.Web);

    public static IServiceCollection AddHealthNsCreative(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: [TagSiap]);
        return services;
    }

    public static void MapHealthNsCreative(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false, ResponseWriter = TulisAsync })
            .AllowAnonymous()
            .DisableRateLimiting();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = c => c.Tags.Contains(TagSiap),
                ResponseWriter = TulisAsync,
            })
            .AllowAnonymous()
            .DisableRateLimiting();
    }

    private static Task TulisAsync(HttpContext http, HealthReport laporan)
    {
        http.Response.ContentType = "application/json";
        return http.Response.WriteAsJsonAsync(new
        {
            status = laporan.Status.ToString(),
            durasiMs = (int)laporan.TotalDuration.TotalMilliseconds,
            checks = laporan.Entries.Select(e => new
            {
                nama = e.Key,
                status = e.Value.Status.ToString(),
                durasiMs = (int)e.Value.Duration.TotalMilliseconds,
                keterangan = e.Value.Description,
            }),
        }, JsonOpsi);
    }
}

/// <summary>Sehat jika database terhubung; Degraded jika masih ada migration yang belum diterapkan.</summary>
public class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            if (!await db.Database.CanConnectAsync(ct))
                return HealthCheckResult.Unhealthy("Database tidak dapat dihubungi.");

            var tertunda = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
            return tertunda.Count == 0
                ? HealthCheckResult.Healthy("Database terhubung.")
                : HealthCheckResult.Degraded($"{tertunda.Count} migration belum diterapkan: {string.Join(", ", tertunda)}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database tidak dapat dihubungi.", ex);
        }
    }
}
