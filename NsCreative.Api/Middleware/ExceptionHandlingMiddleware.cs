using System.Text.Json;
using NsCreative.Core.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace NsCreative.Api.Middleware;

/// <summary>
/// Satu-satunya tempat exception diubah jadi respons HTTP (ProblemDetails).
/// Controller tidak memakai try-catch.
/// </summary>
public class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            await TulisProblemAsync(context, ex);
        }
    }

    private async Task TulisProblemAsync(HttpContext context, Exception ex)
    {
        ProblemDetails problem = ex switch
        {
            ValidationException v => new ValidationProblemDetails(KelompokkanError(v))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Data yang dikirim tidak valid.",
                Detail = v.Errors.FirstOrDefault()?.ErrorMessage,
            },
            NotFoundException => Problem(StatusCodes.Status404NotFound, "Data tidak ditemukan.", ex.Message),
            BusinessRuleException => Problem(StatusCodes.Status422UnprocessableEntity, "Permintaan tidak dapat diproses.", ex.Message),
            ForbiddenException => Problem(StatusCodes.Status403Forbidden, "Akses ditolak.", ex.Message),
            UnauthorizedException => Problem(StatusCodes.Status401Unauthorized, "Belum login.", ex.Message),
            _ => Problem(
                StatusCodes.Status500InternalServerError,
                "Terjadi kesalahan di server.",
                // Pesan asli hanya diperlihatkan saat development.
                env.IsDevelopment() ? ex.Message : "Silakan coba lagi atau hubungi admin."),
        };

        if (problem.Status >= 500)
            logger.LogError(ex, "Kesalahan tak tertangani pada {Method} {Path}", context.Request.Method, context.Request.Path);

        problem.Instance = context.Request.Path;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem, problem.GetType(), JsonOpsi);
    }

    private static readonly JsonSerializerOptions JsonOpsi = new(JsonSerializerDefaults.Web);

    private static ProblemDetails Problem(int status, string title, string detail) =>
        new() { Status = status, Title = title, Detail = detail };

    /// <summary>Error per field, dengan nama field camelCase sesuai JSON (nama, telepon, ...).</summary>
    private static Dictionary<string, string[]> KelompokkanError(ValidationException ex) =>
        ex.Errors
            .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
}
