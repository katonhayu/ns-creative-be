using NsCreative.Core.Dtos.Reports;

namespace NsCreative.Core.Common;

/// <summary>
/// Pencetak PDF laporan. Diimplementasikan di project Api (QuestPDF), sehingga Core tidak
/// bergantung pada pustaka PDF. Data dan hak aksesnya disiapkan ReportService.
/// </summary>
public interface IPembuatPdfLaporan
{
    byte[] LaporanEvent(DataLaporanEvent data);

    byte[] RekapLaporan(DataRekapLaporan data);
}

/// <param name="NamaFile">Nama file unduhan, sudah aman untuk sistem file.</param>
public record FilePdf(byte[] Isi, string NamaFile);
