using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Documents;
using NsCreative.Core.Entities;
using NsCreative.Core.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace NsCreative.Core.Services;

public interface IDocumentService
{
    Task<PagedResult<DocumentDto>> GetListAsync(Guid eventId, QueryParams query, string? jenis, CancellationToken ct = default);
    Task<DocumentDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DocumentDto> UploadAsync(Guid eventId, UploadDocumentDto dto, CancellationToken ct = default);
    Task<FileDokumen> DownloadAsync(Guid id, CancellationToken ct = default);
    Task<DocumentDto> UpdateAsync(Guid id, UpdateDocumentDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Dokumen event. Metadata di tabel documents, file fisik lewat <see cref="IPenyimpananFile"/>.
/// Dokumen tetap boleh ditambah setelah event selesai (misalnya foto dan berita acara).
/// </summary>
public class DocumentService(
    IAppDbContext db,
    IAksesEvent akses,
    IPenyimpananFile penyimpanan,
    IValidator<UploadDocumentDto> uploadValidator,
    IValidator<UpdateDocumentDto> updateValidator) : IDocumentService
{
    private static readonly PetaUrutan<Document> Urutan = new PetaUrutan<Document>()
        .Kolom("tanggal", d => d.Tanggal, bawaan: true, lanjutan: d => d.NamaDokumen, menurunBawaan: true)
        .Kolom("namaDokumen", d => d.NamaDokumen)
        .Kolom("jenis", d => d.Jenis, lanjutan: d => d.NamaDokumen)
        .Kolom("ukuranByte", d => d.UkuranByte, lanjutan: d => d.NamaDokumen)
        .Kolom("createdAt", d => d.CreatedAt, menurunBawaan: true);

    public async Task<PagedResult<DocumentDto>> GetListAsync(
        Guid eventId, QueryParams query, string? jenis, CancellationToken ct = default)
    {
        await akses.LihatAsync(eventId, ct);

        var q = db.Documents.AsNoTracking().Where(d => d.EventId == eventId);
        if (Teks.Bersihkan(jenis)?.ToLower() is { } j) q = q.Where(d => d.Jenis.ToLower() == j);

        // Nama unduhan dihitung di C#, jadi dipetakan setelah satu halaman diambil.
        var halaman = await q
            .Cari(query.Search, d => d.NamaDokumen, d => d.Jenis, d => d.Keterangan)
            .Urutkan(query, Urutan)
            .ToPagedResultAsync(query, ct);
        return new PagedResult<DocumentDto>(
            halaman.Items.Select(KeDto).ToList(), halaman.Page, halaman.PageSize, halaman.TotalItems, halaman.TotalPages);
    }

    public async Task<DocumentDto> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        KeDto(await CariAsync(id, ubah: false, ct));

    /// <summary>
    /// Simpan file dulu, baru metadata. Jika penyimpanan metadata gagal, file dihapus lagi
    /// supaya tidak tertinggal file tanpa pemilik.
    /// </summary>
    public async Task<DocumentDto> UploadAsync(Guid eventId, UploadDocumentDto dto, CancellationToken ct = default)
    {
        await uploadValidator.ValidateAndThrowAsync(dto, ct);
        // Dokumen tetap boleh diunggah setelah event selesai (berita acara, foto, invoice).
        await akses.KelolaAsync(eventId, ct);

        var ekstensi = Path.GetExtension(dto.NamaFileAsli!).ToLowerInvariant();
        var isi = await StreamBisaDiulangAsync(dto.Isi!, ct);
        var awal = new byte[8];
        var terbaca = await isi.ReadAtLeastAsync(awal, awal.Length, throwOnEndOfStream: false, ct);
        if (!TipeDokumen.IsiSesuai(ekstensi, awal.AsSpan(0, terbaca)))
            throw new ValidationException(
                [new ValidationFailure("File", $"Isi file tidak sesuai dengan format {ekstensi.TrimStart('.')}.")]);
        isi.Position = 0;

        var path = await penyimpanan.SimpanAsync(eventId, isi, ekstensi, ct);
        var dokumen = new Document
        {
            EventId = eventId,
            NamaDokumen = Teks.Wajib(dto.NamaDokumen),
            Jenis = Teks.Wajib(dto.Jenis),
            Tanggal = dto.Tanggal ?? Waktu.HariIni(),
            FilePath = path,
            UkuranByte = isi.Length,
            ContentType = TipeDokumen.ContentType[ekstensi],
            Keterangan = Teks.Bersihkan(dto.Keterangan),
        };

        try
        {
            db.Documents.Add(dokumen);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            penyimpanan.Hapus(path);
            throw;
        }

        return await GetByIdAsync(dokumen.Id, ct);
    }

    /// <summary>Izin dicek (lewat CariAsync) sebelum file dibuka.</summary>
    public async Task<FileDokumen> DownloadAsync(Guid id, CancellationToken ct = default)
    {
        var dokumen = await CariAsync(id, ubah: false, ct);
        var isi = penyimpanan.Buka(dokumen.FilePath)
            ?? throw new NotFoundException("File dokumen tidak ditemukan di server. Hapus dokumen ini lalu upload ulang.");
        return new FileDokumen(
            isi,
            dokumen.ContentType ?? "application/octet-stream",
            TipeDokumen.NamaUnduhan(dokumen.NamaDokumen, dokumen.FilePath));
    }

    public async Task<DocumentDto> UpdateAsync(Guid id, UpdateDocumentDto dto, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(dto, ct);
        var dokumen = await CariAsync(id, ubah: true, ct);

        dokumen.NamaDokumen = Teks.Wajib(dto.NamaDokumen);
        dokumen.Jenis = Teks.Wajib(dto.Jenis);
        dokumen.Tanggal = dto.Tanggal!.Value;
        dokumen.Keterangan = Teks.Bersihkan(dto.Keterangan);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    /// <summary>Hapus record lalu file fisiknya.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var dokumen = await CariAsync(id, ubah: true, ct);
        db.Documents.Remove(dokumen);
        await db.SaveChangesAsync(ct);
        penyimpanan.Hapus(dokumen.FilePath);
    }

    /// <summary>
    /// Endpoint /documents/{id} tidak membawa eventId, jadi izin dicek lewat event pemilik dokumen:
    /// <paramref name="ubah"/> = PIC event atau admin (dan entity dilacak), selain itu cukup boleh melihat.
    /// </summary>
    private async Task<Document> CariAsync(Guid id, bool ubah, CancellationToken ct)
    {
        var q = ubah ? db.Documents : db.Documents.AsNoTracking();
        var dokumen = await q.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException("Dokumen tidak ditemukan.");
        if (ubah) await akses.KelolaAsync(dokumen.EventId, ct);
        else await akses.LihatAsync(dokumen.EventId, ct);
        return dokumen;
    }

    private static DocumentDto KeDto(Document d) => new(
        d.Id, d.EventId, d.NamaDokumen, d.Jenis, d.Tanggal,
        TipeDokumen.NamaUnduhan(d.NamaDokumen, d.FilePath),
        d.UkuranByte, d.ContentType, d.Keterangan, d.CreatedAt, d.UpdatedAt);

    /// <summary>Stream upload biasanya sudah bisa di-seek; jika tidak, disalin ke memori (maks 10 MB).</summary>
    private static async Task<Stream> StreamBisaDiulangAsync(Stream isi, CancellationToken ct)
    {
        if (isi.CanSeek) return isi;
        var salinan = new MemoryStream();
        await isi.CopyToAsync(salinan, ct);
        salinan.Position = 0;
        return salinan;
    }
}
