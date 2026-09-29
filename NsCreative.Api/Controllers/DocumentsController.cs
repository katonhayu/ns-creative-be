using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Documents;
using NsCreative.Core.Services;
using NsCreative.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace NsCreative.Api.Controllers;

/// <summary>
/// Dokumen event. File disimpan di luar wwwroot dan hanya bisa diambil lewat endpoint download.
/// Dokumen tetap bisa dikelola setelah event selesai.
/// </summary>
[ApiController]
[Authorize(Roles = Peran.AdminPic)]
[Route("api")]
[Produces("application/json")]
public class DocumentsController(IDocumentService documents) : ControllerBase
{
    /// <summary>Batas body sedikit di atas 10 MB supaya file yang terlalu besar mendapat pesan validasi yang jelas.</summary>
    private const long BatasBody = TipeDokumen.UkuranMaks + 1024 * 1024;

    /// <summary>Daftar dokumen event, terbaru lebih dulu.</summary>
    /// <remarks>sortBy: tanggal (default, terbaru dulu), namaDokumen, jenis, ukuranByte, createdAt. Filter <c>jenis</c> sama persis.</remarks>
    [HttpGet("events/{eventId:guid}/documents")]
    [ProducesResponseType<PagedResult<DocumentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<DocumentDto>>> GetList(
        Guid eventId, [FromQuery] QueryParams query, [FromQuery] string? jenis, CancellationToken ct) =>
        Ok(await documents.GetListAsync(eventId, query, jenis, ct));

    /// <summary>Upload dokumen (multipart/form-data). Maksimal 10 MB: pdf, doc, docx, xls, xlsx, jpg, jpeg, png.</summary>
    /// <remarks>Field: file, namaDokumen, jenis, tanggal (opsional, default hari ini), keterangan.</remarks>
    [HttpPost("events/{eventId:guid}/documents")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(BatasBody)]
    [RequestFormLimits(MultipartBodyLengthLimit = BatasBody)]
    [ProducesResponseType<DocumentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentDto>> Upload(Guid eventId, [FromForm] UploadDocumentForm form, CancellationToken ct)
    {
        await using var isi = form.File?.OpenReadStream();
        var hasil = await documents.UploadAsync(eventId, new UploadDocumentDto(
            form.NamaDokumen, form.Jenis, form.Tanggal, form.Keterangan,
            form.File?.FileName, form.File?.Length ?? 0, isi), ct);
        return CreatedAtAction(nameof(GetById), new { id = hasil.Id }, hasil);
    }

    [HttpGet("documents/{id:guid}")]
    [ProducesResponseType<DocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await documents.GetByIdAsync(id, ct));

    /// <summary>Unduh file. Dengan <c>?inline=true</c> file dibuka di browser (untuk pratinjau PDF dan gambar).</summary>
    [HttpGet("documents/{id:guid}/download")]
    [ProducesResponseType<FileStreamResult>(StatusCodes.Status200OK, "application/octet-stream")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid id, [FromQuery] bool inline, CancellationToken ct)
    {
        var file = await documents.DownloadAsync(id, ct);
        if (!inline) return File(file.Isi, file.ContentType, file.NamaFile, enableRangeProcessing: true);

        Response.Headers.ContentDisposition =
            new ContentDispositionHeaderValue("inline") { FileNameStar = file.NamaFile }.ToString();
        return File(file.Isi, file.ContentType, enableRangeProcessing: true);
    }

    /// <summary>Ubah metadata (nama, jenis, tanggal, keterangan). File tidak diganti.</summary>
    [HttpPut("documents/{id:guid}")]
    [ProducesResponseType<DocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentDto>> Update(Guid id, UpdateDocumentDto dto, CancellationToken ct) =>
        Ok(await documents.UpdateAsync(id, dto, ct));

    /// <summary>Hapus record dan file fisiknya.</summary>
    [HttpDelete("documents/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await documents.DeleteAsync(id, ct);
        return NoContent();
    }
}

/// <summary>Field form upload dokumen.</summary>
public class UploadDocumentForm
{
    public IFormFile? File { get; set; }
    public string NamaDokumen { get; set; } = "";
    public string Jenis { get; set; } = "";
    public DateOnly? Tanggal { get; set; }
    public string? Keterangan { get; set; }
}
