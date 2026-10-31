using NsCreative.Core.Entities;
using NsCreative.Core.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace NsCreative.Data.Seed;

/// <summary>
/// Data awal untuk development. Idempoten: setiap baris memakai Guid literal yang
/// tetap, dan hanya disisipkan jika Id-nya belum ada. Aman dijalankan berkali-kali.
/// Semua akun contoh memakai kata sandi <see cref="PasswordContoh"/>.
/// </summary>
public static class DbSeeder
{
    public const string PasswordContoh = "nscreative";

    private static Guid Id(string kelompok, int nomor) => new($"{kelompok}-0000-0000-0000-{nomor:D12}");

    // Kelompok Guid per tabel supaya mudah dikenali saat membaca data.
    private static Guid UserId(int n) => Id("11111111", n);
    private static Guid ClientId(int n) => Id("22222222", n);
    private static Guid TalentId(int n) => Id("33333333", n);
    private static Guid VendorId(int n) => Id("44444444", n);
    private static Guid CrewId(int n) => Id("55555555", n);
    private static Guid EventId(int n) => Id("66666666", n);

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        var hasher = new PasswordHasher<User>();
        User Akun(int n, string nama, string email, UserRole role)
        {
            var u = new User { Id = UserId(n), Nama = nama, Email = email, Role = role, IsAktif = true };
            u.PasswordHash = hasher.HashPassword(u, PasswordContoh);
            return u;
        }

        await TambahYangBelumAda(db.Users, ct,
        [
            Akun(1, "Rizal Pratama", "rizal@nscreative.id", UserRole.Admin),
            Akun(2, "Dewi Lestari", "dewi@nscreative.id", UserRole.Crew),
            Akun(3, "Fitri Handayani", "fitri@nscreative.id", UserRole.Crew),
            Akun(4, "Hendra Gunawan", "hendra@nscreative.id", UserRole.EventManager),
        ]);

        await TambahYangBelumAda(db.Clients, ct,
        [
            new Client
            {
                Id = ClientId(1), Nama = "Andi Wijaya", Perusahaan = "PT Nusantara Digital Solusi",
                Telepon = "0812-2233-4455", Email = "andi.wijaya@nusantaradigital.co.id",
                Alamat = "Jl. Asia Afrika No. 88, Bandung",
            },
            new Client
            {
                Id = ClientId(2), Nama = "Siti Rahmawati", Perusahaan = "PT Bank Mitra Parahyangan",
                Telepon = "0813-9876-1122", Email = "siti.rahmawati@bankmitra.co.id",
                Alamat = "Jl. Braga No. 12, Bandung",
            },
            new Client
            {
                Id = ClientId(3), Nama = "Rina Kartika", Perusahaan = null,
                Telepon = "0857-1234-9087", Email = "rina.kartika@gmail.com",
                Alamat = "Jl. Dago Pojok No. 5, Bandung",
            },
        ]);

        await TambahYangBelumAda(db.Talents, ct,
        [
            new Talent
            {
                Id = TalentId(1), Nama = "Andi Pratama", Jenis = "MC", ContactPerson = "Rina",
                Telepon = "0812-1111-2222", FeeStandar = 15_000_000m,
                RiderDefault = "2 mic wireless, ruang tunggu ber-AC, air mineral.",
            },
            new Talent
            {
                Id = TalentId(2), Nama = "Salsa Dewi", Jenis = "Penyanyi", ContactPerson = "Budi",
                Telepon = "0813-3333-4444", FeeStandar = 8_000_000m,
                RiderDefault = "In-ear monitor, 1 kursi, handuk kecil.",
            },
            new Talent
            {
                Id = TalentId(3), Nama = "Senandung Band", Jenis = "Band", ContactPerson = "Yoga Aditya",
                Telepon = "0821-5566-7788", FeeStandar = 25_000_000m,
                RiderDefault = "Sound system min. 10.000 watt, drum set lengkap, 4 monitor panggung.",
            },
            new Talent
            {
                Id = TalentId(4), Nama = "DJ Reza Mahendra", Jenis = "DJ", ContactPerson = null,
                Telepon = "0857-2020-3131", FeeStandar = 12_000_000m,
                RiderDefault = "CDJ-3000 x2, mixer DJM-900, meja DJ 1,5 m.",
            },
            new Talent
            {
                Id = TalentId(5), Nama = "Sanggar Tari Puspa Kencana", Jenis = "Penari", ContactPerson = "Bu Euis",
                Telepon = "0822-4040-5151", FeeStandar = 7_500_000m,
                RiderDefault = "Ruang ganti untuk 12 penari, cermin besar, 2 meja rias.",
            },
        ]);

        await TambahYangBelumAda(db.Vendors, ct,
        [
            new Vendor
            {
                Id = VendorId(1), Nama = "PT Soundwave Pro Audio", JenisJasa = "Sound & Lighting",
                ContactPerson = "Rudi", Telepon = "0812-8888-9999",
            },
            new Vendor
            {
                Id = VendorId(2), Nama = "Dapur Nusantara Catering", JenisJasa = "Catering",
                ContactPerson = "Bu Sari", Telepon = "0813-1111-2222",
            },
            new Vendor
            {
                Id = VendorId(3), Nama = "FotoKita Studio", JenisJasa = "Dokumentasi",
                ContactPerson = "Dimas", Telepon = "0811-3333-4444",
            },
            new Vendor
            {
                Id = VendorId(4), Nama = "Tenda Megah Bandung", JenisJasa = "Tenda & Dekorasi",
                ContactPerson = "Pak Ujang", Telepon = "0822-6060-7171",
            },
            new Vendor
            {
                Id = VendorId(5), Nama = "Visual Kreasi LED", JenisJasa = "LED Screen",
                ContactPerson = "Andre", Telepon = "0812-6363-7474",
            },
        ]);

        // Dewi dan Fitri memegang event contoh, jadi data crew-nya ditautkan ke akun login.
        // Database lama yang dimigrasikan dari peran pic sudah punya crew untuk akun ini
        // (dibuat migration RemovePicRole), jadi crew bertaut akun dilewati jika akunnya sudah dipakai.
        var akunBertaut = await db.Crews.IgnoreQueryFilters()
            .Where(c => c.UserId != null)
            .Select(c => c.UserId)
            .ToListAsync(ct);
        await TambahYangBelumAda(db.Crews, ct,
        new Crew[]
        {
            new() { Id = CrewId(1), Nama = "Bagas Saputra", Telepon = "0812-7777-8888", Keahlian = "Stage Manager" },
            new() { Id = CrewId(2), Nama = "Yoga Permana", Telepon = "0813-0999-0000", Keahlian = "Lighting" },
            new() { Id = CrewId(3), Nama = "Fajar Nugraha", Telepon = "0821-4455-6677", Keahlian = "Sound Engineer" },
            new() { Id = CrewId(4), Nama = "Tika Maharani", Telepon = "0822-5566-7788", Keahlian = "Liaison Officer" },
            new() { Id = CrewId(5), Nama = "Sinta Ayu Lestari", Telepon = "0896-3434-5656", Keahlian = "Registrasi & Usher" },
            new()
            {
                Id = CrewId(6), UserId = UserId(2), Nama = "Dewi Lestari", Telepon = "0812-3030-4040",
                Keahlian = "Project Manager",
            },
            new()
            {
                Id = CrewId(7), UserId = UserId(3), Nama = "Fitri Handayani", Telepon = "0813-5050-6060",
                Keahlian = "Event Coordinator",
            },
        }.Where(c => c.UserId is null || !akunBertaut.Contains(c.UserId)).ToList());

        await TambahYangBelumAda(db.Events, ct,
        [
            new Event
            {
                Id = EventId(1), ClientId = ClientId(1), PicCrewId = CrewId(6),
                Nama = "Annual Meeting PT Nusantara Digital Solusi 2026", Jenis = "Corporate Event",
                TanggalMulai = new DateOnly(2026, 12, 18), TanggalSelesai = new DateOnly(2026, 12, 18),
                Lokasi = "Grand Ballroom Hotel Parahyangan Permai, Bandung",
                Deskripsi = "Rapat tahunan dan malam apresiasi karyawan, sekitar 350 peserta.",
                Status = EventStatus.Dikonfirmasi,
            },
            new Event
            {
                Id = EventId(2), ClientId = ClientId(2), PicCrewId = CrewId(7),
                Nama = "Family Gathering Bank Mitra Parahyangan", Jenis = "Gathering",
                TanggalMulai = new DateOnly(2026, 11, 20), TanggalSelesai = new DateOnly(2026, 11, 22),
                Lokasi = "Grafika Cikole, Lembang",
                Deskripsi = "Gathering karyawan dan keluarga, outbound, dan malam keakraban. 280 peserta.",
                Status = EventStatus.Perencanaan,
            },
        ]);

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Sisipkan baris yang Id-nya belum ada, termasuk yang sudah di-soft delete.</summary>
    private static async Task TambahYangBelumAda<T>(DbSet<T> set, CancellationToken ct, IReadOnlyList<T> data)
        where T : BaseEntity
    {
        var ids = data.Select(d => d.Id).ToList();
        var sudahAda = await set.IgnoreQueryFilters()
            .Where(e => ids.Contains(e.Id))
            .Select(e => e.Id)
            .ToListAsync(ct);
        set.AddRange(data.Where(d => !sudahAda.Contains(d.Id)));
    }
}
