namespace NsCreative.Api.Middleware;

/// <summary>
/// Header keamanan dasar untuk API: browser tidak boleh menebak tipe konten, respons tidak
/// boleh dibingkai halaman lain, dan data API tidak disimpan di cache bersama.
/// </summary>
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var h = context.Response.Headers;
        h.XContentTypeOptions = "nosniff";
        h.XFrameOptions = "DENY";
        h["Referrer-Policy"] = "no-referrer";
        if (context.Request.Path.StartsWithSegments("/api"))
            h.CacheControl = "no-store";
        return next(context);
    }
}
