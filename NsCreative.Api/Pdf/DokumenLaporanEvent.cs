using NsCreative.Core.Dtos.Reports;
using NsCreative.Core.Enums;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static NsCreative.Api.Pdf.GayaPdf;

namespace NsCreative.Api.Pdf;

/// <summary>
/// Laporan pertanggungjawaban satu event (A4 tegak): data event, hasil, anggaran, penugasan,
/// rundown, task, kendala, evaluasi, lampiran dokumen, dan kolom tanda tangan.
/// </summary>
internal static class DokumenLaporanEvent
{
    public static byte[] Buat(DataLaporanEvent d, byte[] logo) =>
        Document.Create(dok => dok.Page(page =>
            {
                Halaman(page, landscape: false);
                page.Header().Element(c => Kop(c, logo, "LAPORAN PERTANGGUNGJAWABAN EVENT", d.Event.Nama));
                page.Content().PaddingVertical(4).Column(col =>
                {
                    col.Spacing(16);
                    Identitas(col, d);
                    Ringkasan(col, d);

                    var n = 0;
                    string No(string judul) => $"{++n}. {judul}";

                    Bagian(col, No("Hasil Pelaksanaan"), b => Paragraf(b, d.Narasi.HasilPelaksanaan));
                    Bagian(col, No("Ringkasan Anggaran"), b => AnggaranPerKategori(b, d));
                    Bagian(col, No("Rincian Pos Anggaran"), b => PosAnggaran(b, d));
                    Bagian(col, No("Talent"), b => Talent(b, d));
                    Bagian(col, No("Vendor"), b => Vendor(b, d));
                    Bagian(col, No("Crew Bertugas"), b => Crew(b, d));
                    Bagian(col, No("Rundown Pelaksanaan"), b => Rundown(b, d));
                    Bagian(col, No("Task Persiapan"), b => Tugas(b, d));
                    Bagian(col, No("Kendala"), b => Paragraf(b, d.Narasi.Kendala, "Tidak ada kendala yang dicatat."));
                    Bagian(col, No("Evaluasi"), b => Paragraf(b, d.Narasi.Evaluasi));
                    if (!string.IsNullOrWhiteSpace(d.Narasi.Catatan))
                        Bagian(col, No("Catatan"), b => Paragraf(b, d.Narasi.Catatan));
                    Bagian(col, No("Lampiran Dokumen"), b => Dokumen(b, d));

                    TandaTangan(col, d);
                });
                page.Footer().Element(c => Kaki(c, d.Cetakan));
            }))
            .WithMetadata(new DocumentMetadata
            {
                Title = $"Laporan Pertanggungjawaban {d.Event.Nama}",
                Author = "NS Creative",
                Subject = "Laporan pertanggungjawaban event",
            })
            .GeneratePdf();

    private static void Identitas(ColumnDescriptor col, DataLaporanEvent d)
    {
        var e = d.Event;
        col.Item().Column(judul =>
        {
            judul.Item().Text(e.Nama).FontSize(17).Bold();
            judul.Item().Text($"{e.Jenis} · {Rentang(e.TanggalMulai, e.TanggalSelesai)} · {Isi(e.Lokasi)}")
                .FontColor(Teks2);
        });

        col.Item().Row(row =>
        {
            row.Spacing(10);
            Kartu(row.RelativeItem(), "Client", k =>
            {
                k.Item().Text(e.NamaClient).SemiBold();
                if (e.PerusahaanClient is { } perusahaan) k.Item().Text(perusahaan).FontColor(Teks2);
                Info(k, "Telepon", e.TeleponClient);
                Info(k, "Email", e.EmailClient);
            });
            Kartu(row.RelativeItem(), "PIC Event", k =>
            {
                k.Item().Text(e.NamaPic).SemiBold();
                Info(k, "Telepon", e.TeleponPic);
                Info(k, "Email", e.EmailPic);
            });
            Kartu(row.RelativeItem(), "Laporan", k =>
            {
                Info(k, "Status event", Label(e.Status));
                Info(k, "Dibuat", Tanggal(d.Narasi.TanggalDibuat));
                Info(k, "Dokumen", $"{d.Dokumen.Count} berkas");
            });
        });

        if (!string.IsNullOrWhiteSpace(e.Deskripsi))
            col.Item().Text(t =>
            {
                t.Span("Deskripsi: ").FontColor(Teks2);
                t.Span(e.Deskripsi.Trim());
            });
    }

    private static void Kartu(IContainer c, string judul, Action<ColumnDescriptor> isi) =>
        c.Border(1).BorderColor(Garis).Padding(8).Column(k =>
        {
            k.Spacing(2);
            k.Item().Text(judul.ToUpperInvariant()).FontSize(7.5f).SemiBold().FontColor(Teks3);
            isi(k);
        });

    private static void Ringkasan(ColumnDescriptor col, DataLaporanEvent d)
    {
        var estimasi = d.PosBudget.Sum(p => p.Estimasi);
        var aktualKini = d.PosBudget.Sum(p => p.Aktual);
        var total = d.Narasi.TotalPengeluaran;
        var sisa = estimasi - total;

        KotakAngka(col,
            ("Estimasi anggaran", Rupiah(estimasi), null),
            ("Total pengeluaran", Rupiah(total), null),
            (sisa >= 0 ? "Sisa anggaran" : "Melebihi anggaran", Rupiah(Math.Abs(sisa)), sisa >= 0 ? Hijau : Merah),
            ("Task selesai", $"{d.Task.Selesai} dari {d.Task.Total}", null));

        if (aktualKini != total)
            col.Item().Background(AksenLembut).Padding(8).Text(
                $"Total pengeluaran dibekukan saat laporan dibuat ({Tanggal(d.Narasi.TanggalDibuat)}). " +
                $"Biaya aktual di budget saat dicetak {Rupiah(aktualKini)}, berbeda {Rupiah(Math.Abs(aktualKini - total))}. " +
                "Tabel anggaran di bawah memakai angka saat dicetak.")
                .FontSize(8.5f).FontColor(Aksen);
    }

    private static void AnggaranPerKategori(ColumnDescriptor col, DataLaporanEvent d)
    {
        if (d.AnggaranPerKategori.Count == 0)
        {
            Kosong(col, "Belum ada pos anggaran.");
            return;
        }

        var estimasi = d.AnggaranPerKategori.Sum(k => k.Estimasi);
        var aktual = d.AnggaranPerKategori.Sum(k => k.Aktual);
        var sponsorship = d.AnggaranPerKategori.Sum(k => k.Sponsorship);
        Tabel(col,
            [new("Kategori", 2), new("Estimasi", 1.4f, true), new("Aktual", 1.4f, true),
             new("Selisih", 1.4f, true), new("Sponsorship", 1.4f, true)],
            d.AnggaranPerKategori.Select(k => new[]
            {
                k.Kategori, Rupiah(k.Estimasi), Rupiah(k.Aktual), Selisih(k.Estimasi - k.Aktual), Rupiah(k.Sponsorship),
            }),
            ["Total", Rupiah(estimasi), Rupiah(aktual), Selisih(estimasi - aktual), Rupiah(sponsorship)]);

        col.Item().Text(t =>
        {
            t.Span("Pengeluaran bersih (aktual − sponsorship): ").FontColor(Teks2);
            t.Span(Rupiah(aktual - sponsorship)).SemiBold();
        });
    }

    private static void PosAnggaran(ColumnDescriptor col, DataLaporanEvent d)
    {
        if (d.PosBudget.Count == 0)
        {
            Kosong(col, "Belum ada pos anggaran.");
            return;
        }

        var no = 0;
        Tabel(col,
            [new("No", Tetap: 24), new("Kebutuhan", 2.4f), new("Kategori", 1.1f), new("Estimasi", 1.4f, true),
             new("Aktual", 1.4f, true), new("Selisih", 1.4f, true), new("Pembayaran", 1.4f)],
            d.PosBudget.Select(p => new[]
            {
                $"{++no}", p.NamaKebutuhan, p.Kategori, Rupiah(p.Estimasi),
                p.Aktual > 0 ? Rupiah(p.Aktual) : "—",
                // Sama dengan aplikasi: selisih hanya untuk pos yang sudah punya biaya aktual.
                p.Aktual > 0 ? Selisih(p.Estimasi - p.Aktual) : "—",
                Label(p.StatusPembayaran),
            }));
        col.Item().Text("Selisih positif berarti hemat, negatif berarti melebihi estimasi.")
            .FontSize(8).FontColor(Teks3);
    }

    private static void Talent(ColumnDescriptor col, DataLaporanEvent d)
    {
        if (d.Talent.Count == 0)
        {
            Kosong(col, "Tidak ada talent di event ini.");
            return;
        }

        var no = 0;
        Tabel(col,
            [new("No", Tetap: 24), new("Talent", 2), new("Jenis", 1.1f), new("Jadwal tampil", 1.7f),
             new("Durasi", 1.1f), new("Fee deal", 1.4f, true), new("Status", 1.1f)],
            d.Talent.Select(t => new[]
            {
                $"{++no}", t.Nama, t.Jenis,
                t.JadwalTampil is { } j ? TanggalJam(j) : "—",
                t.DurasiMenit is { } m ? $"{m} menit" : "—",
                Rupiah(t.FeeDeal), Label(t.Status),
            }),
            ["", "Total fee", "", "", "", Rupiah(d.Talent.Where(t => t.Status != TalentStatus.Batal).Sum(t => t.FeeDeal)), ""]);
        if (d.Talent.Any(t => t.Status == TalentStatus.Batal))
            col.Item().Text("Total fee tidak menghitung talent berstatus Batal.").FontSize(8).FontColor(Teks3);
    }

    private static void Vendor(ColumnDescriptor col, DataLaporanEvent d)
    {
        if (d.Vendor.Count == 0)
        {
            Kosong(col, "Tidak ada vendor di event ini.");
            return;
        }

        var no = 0;
        Tabel(col,
            [new("No", Tetap: 24), new("Vendor", 2), new("Jenis jasa", 1.4f), new("Kebutuhan", 2),
             new("Biaya", 1.4f, true), new("Pembayaran", 1.1f)],
            d.Vendor.Select(v => new[]
            {
                $"{++no}", v.Nama, v.JenisJasa, Isi(v.Kebutuhan), Rupiah(v.Biaya), Label(v.StatusPembayaran),
            }),
            ["", "Total biaya vendor", "", "", Rupiah(d.Vendor.Sum(v => v.Biaya)), ""]);
    }

    private static void Crew(ColumnDescriptor col, DataLaporanEvent d)
    {
        if (d.Crew.Count == 0)
        {
            Kosong(col, "Tidak ada crew yang ditugaskan.");
            return;
        }

        var no = 0;
        Tabel(col,
            [new("No", Tetap: 24), new("Nama", 2.2f), new("Posisi", 2), new("Tanggal bertugas", 1.6f)],
            d.Crew.Select(c => new[]
            {
                $"{++no}", c.Nama, c.Posisi,
                c.TanggalBertugas is { } t ? Tanggal(t) : "Selama event",
            }));
    }

    /// <summary>Dikelompokkan per hari: baris judul hari membentang selebar tabel.</summary>
    private static void Rundown(ColumnDescriptor col, DataLaporanEvent d)
    {
        if (d.Rundown.Count == 0)
        {
            Kosong(col, "Rundown tidak diisi.");
            return;
        }

        col.Item().Table(tabel =>
        {
            tabel.ColumnsDefinition(def =>
            {
                def.ConstantColumn(78);
                def.RelativeColumn(3);
                def.RelativeColumn(1.6f);
                def.RelativeColumn(1.4f);
            });
            tabel.Header(kepala =>
            {
                foreach (var judul in new[] { "Waktu", "Kegiatan", "Lokasi", "PIC" })
                    kepala.Cell().Background(AksenLembut).PaddingVertical(5).PaddingHorizontal(5)
                        .Text(judul).FontSize(8.5f).SemiBold().FontColor(Aksen);
            });

            foreach (var hari in d.Rundown.GroupBy(s => s.Tanggal))
            {
                tabel.Cell().ColumnSpan(4).Background(Permukaan).PaddingVertical(4).PaddingHorizontal(5)
                    .Text(Hari(hari.Key)).SemiBold();
                foreach (var s in hari)
                {
                    var waktu = s.WaktuSelesai is { } selesai ? $"{Jam(s.WaktuMulai)}–{Jam(selesai)}" : Jam(s.WaktuMulai);
                    foreach (var isi in new[] { waktu, s.Kegiatan, Isi(s.Lokasi), Isi(s.NamaPic) })
                        tabel.Cell().BorderBottom(1).BorderColor(Garis).PaddingVertical(4).PaddingHorizontal(5).Text(isi);
                }
            }
        });
    }

    private static void Tugas(ColumnDescriptor col, DataLaporanEvent d)
    {
        var t = d.Task;
        if (t.Total == 0)
        {
            Kosong(col, "Tidak ada task yang dicatat.");
            return;
        }

        var belum = t.Total - t.Selesai;
        col.Item().Text(teks =>
        {
            teks.Span($"{t.Total} task, ");
            teks.Span($"{t.Selesai} selesai ({Persen(t.Selesai, t.Total)})").SemiBold();
            if (belum > 0)
            {
                teks.Span($", {belum} belum selesai");
                if (t.Terlambat > 0) teks.Span($" ({t.Terlambat} melewati deadline)").FontColor(Merah);
            }
            teks.Span(".");
        });
    }

    private static void Dokumen(ColumnDescriptor col, DataLaporanEvent d)
    {
        if (d.Dokumen.Count == 0)
        {
            Kosong(col, "Tidak ada dokumen yang diunggah.");
            return;
        }

        var no = 0;
        Tabel(col,
            [new("No", Tetap: 24), new("Nama dokumen", 3), new("Jenis", 1.4f), new("Tanggal", 1.3f)],
            d.Dokumen.Select(x => new[] { $"{++no}", x.Nama, x.Jenis, TanggalPendek(x.Tanggal) }));
        col.Item().Text("Berkas asli tersimpan di aplikasi, tab Dokumen pada detail event.")
            .FontSize(8).FontColor(Teks3);
    }

    /// <summary>Tiga kolom tanda tangan, selalu utuh dalam satu halaman.</summary>
    private static void TandaTangan(ColumnDescriptor col, DataLaporanEvent d) =>
        col.Item().PaddingTop(10).ShowEntire().Row(row =>
        {
            row.Spacing(24);
            Ttd(row.RelativeItem(), "Disusun oleh,", "PIC Event", d.Event.NamaPic);
            Ttd(row.RelativeItem(), "Mengetahui,", "Manajemen NS Creative", null);
            Ttd(row.RelativeItem(), "Diterima oleh,", "Client", d.Event.PerusahaanClient ?? d.Event.NamaClient);
        });

    private static void Ttd(IContainer c, string pembuka, string jabatan, string? nama) =>
        c.Column(k =>
        {
            k.Item().Text(pembuka).FontColor(Teks2);
            k.Item().Text(jabatan).SemiBold();
            k.Item().Height(56);
            k.Item().LineHorizontal(0.75f).LineColor(Teks3);
            k.Item().PaddingTop(3).Text(nama ?? "Nama jelas").FontColor(nama is null ? Teks3 : Teks);
        });
}
