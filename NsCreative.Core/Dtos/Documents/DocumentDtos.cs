namespace NsCreative.Core.Dtos.Documents;

/// <summary>Metadata dokumen event. File diambil lewat <c>GET /api/documents/{id}/download</c>.</summary>
/// <param name="NamaFile">Nama file saat diunduh: nama dokumen + ekstensi, misalnya "Kontrak Band.pdf".</param>
public record DocumentDto(
    Guid Id,
    Guid EventId,
    string NamaDokumen,
    string Jenis,
    DateOnly Tanggal,
    string NamaFile,
    long UkuranByte,
    string? ContentType,
    string? Keterangan,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Isi form upload setelah dibaca controller. Stream dan nama file asli berasal dari IFormFile.</summary>
/// <param name="Tanggal">Opsional, default hari ini (WIB).</param>
/// <param name="NamaFileAsli">Hanya dipakai untuk membaca ekstensi; file disimpan dengan nama Guid.</param>
public record UploadDocumentDto(
    string NamaDokumen,
    string Jenis,
    DateOnly? Tanggal,
    string? Keterangan,
    string? NamaFileAsli,
    long Ukuran,
    Stream? Isi);

/// <summary>Ubah metadata saja. Untuk mengganti file, hapus lalu upload ulang.</summary>
public record UpdateDocumentDto(string NamaDokumen, string Jenis, DateOnly? Tanggal, string? Keterangan);

/// <summary>File yang siap dikirim ke klien.</summary>
public record FileDokumen(Stream Isi, string ContentType, string NamaFile);
