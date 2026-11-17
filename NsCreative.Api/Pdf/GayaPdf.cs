using System.Globalization;
using NsCreative.Core.Dtos.Reports;
using NsCreative.Core.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NsCreative.Api.Pdf;

/// <summary>Warna, format angka/tanggal, dan potongan tata letak yang dipakai semua laporan PDF.</summary>
internal static class GayaPdf
{
    // Sama dengan token warna frontend (main.css).
    public static readonly Color Teks = Color.FromHex("#141C2E");
    public static readonly Color Teks2 = Color.FromHex("#515D72");
    public static readonly Color Teks3 = Color.FromHex("#8A94A6");
    public static readonly Color Garis = Color.FromHex("#E4E9F2");
    public static readonly Color Permukaan = Color.FromHex("#F6F8FB");
    public static readonly Color Aksen = Color.FromHex("#1D4ED8");
    public static readonly Color AksenLembut = Color.FromHex("#E8EFFF");
    public static readonly Color Merah = Color.FromHex("#B42323");
    public static readonly Color Hijau = Color.FromHex("#177245");

    private static readonly CultureInfo Id = CultureInfo.GetCultureInfo("id-ID");

    // --- Format ---------------------------------------------------------------

    public static string Rupiah(decimal nilai) =>
        nilai < 0 ? $"−Rp {(-nilai).ToString("N0", Id)}" : $"Rp {nilai.ToString("N0", Id)}";

    /// <summary>Selisih bertanda: +Rp … berarti hemat, −Rp … berarti melebihi estimasi.</summary>
    public static string Selisih(decimal nilai) => nilai > 0 ? "+" + Rupiah(nilai) : Rupiah(nilai);

    /// <summary>"83,3%". Nol jika penyebutnya nol.</summary>
    public static string Persen(int bagian, int total) =>
        (total == 0 ? 0 : Math.Round(bagian * 100.0 / total, 1)).ToString("0.#", Id) + "%";

    public static string Tanggal(DateOnly t) => t.ToString("d MMMM yyyy", Id);

    public static string TanggalPendek(DateOnly t) => t.ToString("d MMM yyyy", Id);

    public static string Hari(DateOnly t) => t.ToString("dddd, d MMMM yyyy", Id);

    public static string Jam(TimeOnly t) => t.ToString("HH:mm", Id);

    public static string TanggalJam(DateTime t) => t.ToString("d MMM yyyy, HH:mm", Id);

    /// <summary>"18 Desember 2026" atau "20–22 November 2026" atau "30 Nov – 2 Des 2026".</summary>
    public static string Rentang(DateOnly mulai, DateOnly? selesai)
    {
        if (selesai is not { } s || s == mulai) return Tanggal(mulai);
        if (mulai.Year == s.Year && mulai.Month == s.Month) return $"{mulai.Day}–{Tanggal(s)}";
        if (mulai.Year == s.Year) return $"{mulai.ToString("d MMM", Id)} – {TanggalPendek(s)}";
        return $"{TanggalPendek(mulai)} – {TanggalPendek(s)}";
    }

    /// <summary>"1 Januari 2026 – 31 Desember 2026", "Sejak 1 Maret 2026", atau "Semua periode".</summary>
    public static string Periode(DateOnly? dari, DateOnly? sampai) => (dari, sampai) switch
    {
        ({ } d, { } s) when d == s => Tanggal(d),
        ({ } d, { } s) => $"{Tanggal(d)} – {Tanggal(s)}",
        ({ } d, null) => $"Sejak {Tanggal(d)}",
        (null, { } s) => $"Sampai {Tanggal(s)}",
        _ => "Semua periode",
    };

    public static string Isi(string? teks) => string.IsNullOrWhiteSpace(teks) ? "—" : teks.Trim();

    public static string Label(StatusPembayaran s) => s switch
    {
        StatusPembayaran.BelumBayar => "Belum bayar",
        StatusPembayaran.Dp => "DP",
        _ => "Lunas",
    };

    public static string Label(TalentStatus s) => s switch
    {
        TalentStatus.Dihubungi => "Dihubungi",
        TalentStatus.Deal => "Deal",
        TalentStatus.Kontrak => "Kontrak",
        TalentStatus.Selesai => "Selesai",
        _ => "Batal",
    };

    public static string Label(EventStatus s) => s switch
    {
        EventStatus.Draft => "Draft",
        EventStatus.Perencanaan => "Perencanaan",
        EventStatus.Dikonfirmasi => "Dikonfirmasi",
        EventStatus.Berlangsung => "Berlangsung",
        EventStatus.Selesai => "Selesai",
        _ => "Dibatalkan",
    };

    // --- Kerangka halaman -------------------------------------------------------

    public static void Halaman(PageDescriptor page, bool landscape)
    {
        page.Size(landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
        page.Margin(1.6f, Unit.Centimetre);
        page.PageColor(Color.FromHex("#FFFFFF"));
        page.DefaultTextStyle(t => t.FontSize(9.5f).FontColor(Teks).LineHeight(1.25f));
    }

    /// <summary>Kop setiap halaman: logo, nama perusahaan, judul laporan, dan subjudul.</summary>
    public static void Kop(IContainer c, byte[] logo, string judul, string subjudul) =>
        c.PaddingBottom(10).Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(52).Height(40).Image(logo).FitArea();
                row.ConstantItem(10);
                row.RelativeItem().AlignMiddle().Column(teks =>
                {
                    teks.Item().Text("NS Creative · Event Production Agency").FontSize(8).FontColor(Teks3);
                    teks.Item().Text(judul).FontSize(12).Bold().FontColor(Aksen);
                    teks.Item().Text(subjudul).FontSize(9).FontColor(Teks2);
                });
            });
            col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Garis);
        });

    /// <summary>Kaki setiap halaman: siapa dan kapan dicetak, serta nomor halaman.</summary>
    public static void Kaki(IContainer c, CetakanLaporan cetakan) =>
        c.PaddingTop(6).BorderTop(1).BorderColor(Garis).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text($"Dicetak oleh {cetakan.Oleh}, {TanggalJam(cetakan.Pada)} WIB")
                .FontSize(7.5f).FontColor(Teks3);
            row.AutoItem().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Teks3));
                t.Span("Halaman ");
                t.CurrentPageNumber();
                t.Span(" dari ");
                t.TotalPages();
            });
        });

    // --- Potongan isi -------------------------------------------------------------

    /// <summary>Judul bagian bernomor. Bagian tidak dimulai di dasar halaman tanpa isinya.</summary>
    public static void Bagian(ColumnDescriptor col, string judul, Action<ColumnDescriptor> isi) =>
        col.Item().EnsureSpace(70).Column(bagian =>
        {
            bagian.Spacing(6);
            bagian.Item().Text(judul).FontSize(11).SemiBold().FontColor(Aksen);
            isi(bagian);
        });

    public static void Paragraf(ColumnDescriptor col, string? teks, string kosong = "Belum diisi.")
    {
        if (string.IsNullOrWhiteSpace(teks)) col.Item().Text(kosong).Italic().FontColor(Teks3);
        else col.Item().Text(teks.Trim()).FontColor(Teks).Justify();
    }

    public static void Kosong(ColumnDescriptor col, string pesan) =>
        col.Item().Text(pesan).Italic().FontColor(Teks3);

    /// <summary>Kotak angka ringkasan berjajar.</summary>
    public static void KotakAngka(ColumnDescriptor col, params (string Label, string Nilai, Color? Warna)[] kotak) =>
        col.Item().Row(row =>
        {
            row.Spacing(8);
            foreach (var (label, nilai, warna) in kotak)
            {
                row.RelativeItem().Background(Permukaan).Border(1).BorderColor(Garis).Padding(8).Column(k =>
                {
                    k.Item().Text(label).FontSize(8).FontColor(Teks2);
                    k.Item().Text(nilai).FontSize(12).Bold().FontColor(warna ?? Teks);
                });
            }
        });

    /// <param name="Relatif">Lebar relatif. Diabaikan jika <paramref name="Tetap"/> diisi (dalam point).</param>
    public record Kolom(string Judul, float Relatif = 1, bool Kanan = false, float? Tetap = null);

    /// <summary>
    /// Tabel dengan kepala yang diulang di setiap halaman. Sel berisi teks yang sudah diformat.
    /// <paramref name="total"/> dicetak tebal sebagai baris penutup.
    /// </summary>
    public static void Tabel(
        ColumnDescriptor col, IReadOnlyList<Kolom> kolom, IEnumerable<string[]> baris, string[]? total = null) =>
        col.Item().Table(tabel =>
        {
            tabel.ColumnsDefinition(def =>
            {
                foreach (var k in kolom)
                {
                    if (k.Tetap is { } tetap) def.ConstantColumn(tetap);
                    else def.RelativeColumn(k.Relatif);
                }
            });

            tabel.Header(kepala =>
            {
                foreach (var k in kolom)
                {
                    var sel = kepala.Cell().Background(AksenLembut).PaddingVertical(5).PaddingHorizontal(5);
                    (k.Kanan ? sel.AlignRight() : sel).Text(k.Judul).FontSize(8.5f).SemiBold().FontColor(Aksen);
                }
            });

            foreach (var b in baris)
            {
                for (var i = 0; i < kolom.Count; i++)
                {
                    var sel = tabel.Cell().BorderBottom(1).BorderColor(Garis).PaddingVertical(4).PaddingHorizontal(5);
                    (kolom[i].Kanan ? sel.AlignRight() : sel).Text(i < b.Length ? b[i] : "");
                }
            }

            if (total is null) return;
            for (var i = 0; i < kolom.Count; i++)
            {
                var sel = tabel.Cell().Background(Permukaan).PaddingVertical(5).PaddingHorizontal(5);
                (kolom[i].Kanan ? sel.AlignRight() : sel).Text(i < total.Length ? total[i] : "").SemiBold();
            }
        });

    /// <summary>Pasangan label dan nilai kecil, misalnya di kartu info client.</summary>
    public static void Info(ColumnDescriptor col, string label, string? nilai) =>
        col.Item().Text(t =>
        {
            t.Span($"{label}: ").FontColor(Teks2);
            t.Span(Isi(nilai));
        });
}
