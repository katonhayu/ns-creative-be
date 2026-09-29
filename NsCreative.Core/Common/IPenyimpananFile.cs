namespace NsCreative.Core.Common;

/// <summary>
/// Penyimpanan file dokumen. Path yang dikembalikan relatif terhadap folder upload,
/// berbentuk <c>{eventId}/{guid}.{ekstensi}</c>, dan itulah yang disimpan di documents.file_path.
/// </summary>
public interface IPenyimpananFile
{
    Task<string> SimpanAsync(Guid eventId, Stream isi, string ekstensi, CancellationToken ct = default);

    /// <summary>Null jika file fisiknya tidak ada.</summary>
    Stream? Buka(string path);

    /// <summary>Tidak error jika file sudah tidak ada.</summary>
    void Hapus(string path);

    /// <summary>Hapus seluruh file satu event, dipakai saat event dihapus permanen.</summary>
    void HapusFolderEvent(Guid eventId);
}
