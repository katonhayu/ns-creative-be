using System.Diagnostics;
using NsCreative.Api.Auth;

namespace NsCreative.Api.Middleware;

/// <summary>
/// Satu baris log terstruktur per request: method, path (tanpa query string), status, durasi,
/// dan id pengguna. Di production log ditulis sebagai JSON sehingga field ini bisa difilter.
/// Health check tidak dicatat supaya log tidak penuh.
/// </summary>
public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await next(context);
            return;
        }

        var mulai = Stopwatch.GetTimestamp();
        using var _ = logger.BeginScope(new Dictionary<string, object> { ["TraceId"] = context.TraceIdentifier });
        try
        {
            await next(context);
        }
        finally
        {
            var status = context.Response.StatusCode;
            var level = status >= 500 ? LogLevel.Error : status >= 400 ? LogLevel.Warning : LogLevel.Information;
            logger.Log(level,
                "HTTP {Method} {Path} menghasilkan {StatusCode} dalam {ElapsedMs:0} ms (pengguna {UserId})",
                context.Request.Method,
                context.Request.Path.Value,
                status,
                Stopwatch.GetElapsedTime(mulai).TotalMilliseconds,
                context.User.FindFirst(KlaimJwt.Sub)?.Value ?? "-");
        }
    }
}
