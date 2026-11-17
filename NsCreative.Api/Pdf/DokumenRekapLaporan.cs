using NsCreative.Core.Dtos.Reports;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static NsCreative.Api.Pdf.GayaPdf;

namespace NsCreative.Api.Pdf;

/// <summary>Rekap event selesai pada satu periode (A4 mendatar), sesuai filter halaman Laporan.</summary>
internal static class DokumenRekapLaporan
{
    public static byte[] Buat(DataRekapLaporan d, byte[] logo) =>
        Document.Create(dok => dok.Page(page =>
            {
                Halaman(page, landscape: true);
                page.Header().Element(c => Kop(c, logo, "REKAP LAPORAN EVENT", $"Periode {Periode(d.Dari, d.Sampai)}"));
                page.Content().PaddingVertical(4).Column(col =>
                {
                    col.Spacing(14);
                    Judul(col, d);
                    Ringkasan(col, d);
                    Daftar(col, d);
                });
                page.Footer().Element(c => Kaki(c, d.Cetakan));
            }))
            .WithMetadata(new DocumentMetadata
            {
                Title = $"Rekap Laporan Event, {Periode(d.Dari, d.Sampai)}",
                Author = "NS Creative",
                Subject = "Rekap laporan event",
            })
            .GeneratePdf();

    private static void Judul(ColumnDescriptor col, DataRekapLaporan d) =>
        col.Item().Column(judul =>
        {
            judul.Item().Text("Rekap Laporan Event").FontSize(16).Bold();
            judul.Item().Text(t =>
            {
                t.Span("Periode: ").FontColor(Teks2);
                t.Span(Periode(d.Dari, d.Sampai)).SemiBold();
            });
            judul.Item().Text("Event berstatus selesai yang rampung pada periode ini, beserta laporan pertanggungjawabannya.")
                .FontColor(Teks2);
            foreach (var f in d.Filter) judul.Item().Text($"Filter: {f}").FontSize(8.5f).FontColor(Teks3);
        });

    private static void Ringkasan(ColumnDescriptor col, DataRekapLaporan d)
    {
        var sudah = d.Baris.Count(b => b.TanggalLaporan is not null);
        var belum = d.Baris.Count - sudah;
        KotakAngka(col,
            ("Event selesai", $"{d.Baris.Count}", null),
            ("Laporan sudah dibuat", $"{sudah}", Hijau),
            ("Laporan belum dibuat", $"{belum}", belum > 0 ? Merah : (Color?)null),
            ("Total pengeluaran", Rupiah(d.Baris.Sum(b => b.TotalPengeluaran ?? 0)), null));
    }

    private static void Daftar(ColumnDescriptor col, DataRekapLaporan d)
    {
        if (d.Baris.Count == 0)
        {
            Kosong(col, "Tidak ada event selesai yang cocok dengan filter ini.");
            return;
        }

        var no = 0;
        Tabel(col,
            [new("No", Tetap: 26), new("Event", 3), new("Client", 2.2f), new("Tanggal", 1.6f), new("PIC", 1.4f),
             new("Dokumen", 0.8f, true), new("Laporan dibuat", 1.3f), new("Total pengeluaran", 1.6f, true)],
            d.Baris.Select(b => new[]
            {
                $"{++no}",
                b.NamaEvent,
                b.PerusahaanClient is { } p ? $"{p} ({b.NamaClient})" : b.NamaClient,
                Rentang(b.TanggalMulai, b.TanggalSelesai),
                b.NamaPic,
                $"{b.JumlahDokumen}",
                b.TanggalLaporan is { } t ? TanggalPendek(t) : "Belum dibuat",
                b.TotalPengeluaran is { } total ? Rupiah(total) : "—",
            }),
            ["", $"{d.Baris.Count} event", "", "", "", $"{d.Baris.Sum(b => b.JumlahDokumen)}", "",
             Rupiah(d.Baris.Sum(b => b.TotalPengeluaran ?? 0))]);

        col.Item().Text(
            "Total pengeluaran adalah angka yang dibekukan saat laporan dibuat. " +
            "Event yang laporannya belum dibuat tidak ikut dijumlahkan.")
            .FontSize(8).FontColor(Teks3);
    }
}
