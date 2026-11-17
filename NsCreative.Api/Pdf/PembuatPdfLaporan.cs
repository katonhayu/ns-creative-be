using NsCreative.Core.Common;
using NsCreative.Core.Dtos.Reports;

namespace NsCreative.Api.Pdf;

/// <summary>
/// Pencetak PDF laporan dengan QuestPDF. Logo dibaca sekali dari embedded resource.
/// Lisensi QuestPDF diatur di Program.cs.
/// </summary>
public class PembuatPdfLaporan : IPembuatPdfLaporan
{
    private readonly byte[] _logo = BacaLogo();

    public byte[] LaporanEvent(DataLaporanEvent data) => DokumenLaporanEvent.Buat(data, _logo);

    public byte[] RekapLaporan(DataRekapLaporan data) => DokumenRekapLaporan.Buat(data, _logo);

    private static byte[] BacaLogo()
    {
        using var sumber = typeof(PembuatPdfLaporan).Assembly.GetManifestResourceStream("NsCreative.Api.Pdf.logo.png")
            ?? throw new InvalidOperationException("Logo laporan (Pdf/logo.png) tidak ter-embed di assembly.");
        using var salinan = new MemoryStream();
        sumber.CopyTo(salinan);
        return salinan.ToArray();
    }
}
