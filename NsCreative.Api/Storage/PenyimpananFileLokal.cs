using NsCreative.Core.Common;

namespace NsCreative.Api.Storage;

/// <summary>
/// File disimpan di disk, di <c>{ContentRoot}/App_Data/uploads/{eventId}/</c> (bisa diganti lewat
/// konfigurasi <c>Penyimpanan:Folder</c>). Folder ini di luar wwwroot, jadi file hanya bisa
/// diambil lewat endpoint download yang memeriksa izin.
/// </summary>
public class PenyimpananFileLokal : IPenyimpananFile
{
    private readonly string _root;

    public PenyimpananFileLokal(IWebHostEnvironment env, IConfiguration config)
    {
        var folder = config["Penyimpanan:Folder"] ?? Path.Combine("App_Data", "uploads");
        _root = Path.GetFullPath(Path.Combine(env.ContentRootPath, folder));
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SimpanAsync(Guid eventId, Stream isi, string ekstensi, CancellationToken ct = default)
    {
        // Nama file dibuat server (Guid), bukan dari nama asli, jadi aman dari path traversal.
        var relatif = $"{eventId}/{NewId.Create()}{ekstensi.ToLowerInvariant()}";
        var penuh = PathPenuh(relatif);
        Directory.CreateDirectory(Path.GetDirectoryName(penuh)!);

        await using var tujuan = new FileStream(penuh, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await isi.CopyToAsync(tujuan, ct);
        return relatif;
    }

    public Stream? Buka(string path)
    {
        var penuh = PathPenuh(path);
        return File.Exists(penuh)
            ? new FileStream(penuh, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
    }

    public void Hapus(string path)
    {
        var penuh = PathPenuh(path);
        if (File.Exists(penuh)) File.Delete(penuh);
    }

    public void HapusFolderEvent(Guid eventId)
    {
        var folder = PathPenuh(eventId.ToString());
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

    /// <summary>Tolak path yang keluar dari folder upload, misalnya berisi "..".</summary>
    private string PathPenuh(string relatif)
    {
        var penuh = Path.GetFullPath(Path.Combine(_root, relatif));
        if (!penuh.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Path file berada di luar folder upload.");
        return penuh;
    }
}
