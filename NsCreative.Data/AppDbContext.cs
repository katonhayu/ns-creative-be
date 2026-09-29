using NsCreative.Core.Common;
using NsCreative.Core.Entities;
using NsCreative.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NsCreative.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    // Nama tabel diambil dari nama DbSet lalu diubah ke snake_case (event_talents, tasks, ...).
    public DbSet<User> Users => Set<User>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Talent> Talents => Set<Talent>();
    public DbSet<Crew> Crews => Set<Crew>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventTalent> EventTalents => Set<EventTalent>();
    public DbSet<EventCrew> EventCrews => Set<EventCrew>();
    public DbSet<EventVendor> EventVendors => Set<EventVendor>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Client, Talent, Crew, dan Vendor punya soft delete (global query filter) sekaligus
        // menjadi sisi wajib relasi event dan pivot. EF memperingatkan bahwa baris event/pivot
        // ikut hilang dari query saat master-nya sudah dihapus. Itu disengaja untuk daftar
        // master, TAPI query riwayat event (penugasan, budget, laporan) wajib memakai
        // IgnoreQueryFilters() atau proyeksi yang tidak bergantung pada filter tersebut,
        // supaya penugasan lama tetap tampil walau talent/vendornya sudah dihapus.
        optionsBuilder.ConfigureWarnings(w =>
            w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Semua nilai uang: numeric(15, 2).
        builder.Properties<decimal>().HavePrecision(15, 2);

        // Semua enum disimpan sebagai string snake_case, maksimal 30 karakter.
        builder.Properties<UserRole>().HaveConversion<SnakeCaseEnumConverter<UserRole>>().HaveMaxLength(30);
        builder.Properties<EventStatus>().HaveConversion<SnakeCaseEnumConverter<EventStatus>>().HaveMaxLength(30);
        builder.Properties<TalentStatus>().HaveConversion<SnakeCaseEnumConverter<TalentStatus>>().HaveMaxLength(30);
        builder.Properties<StatusPembayaran>()
            .HaveConversion<SnakeCaseEnumConverter<StatusPembayaran>>()
            .HaveMaxLength(30);
        builder.Properties<TaskItemStatus>()
            .HaveConversion<SnakeCaseEnumConverter<TaskItemStatus>>()
            .HaveMaxLength(30);
    }

    // created_at dan updated_at sengaja TIDAK diisi di SaveChanges: database yang mengisinya
    // (DEFAULT now() dan trigger set_updated_at()), lalu EF membacanya kembali lewat RETURNING.
    // Lihat catatan no. 1 di schema-nscreative.sql: pilih salah satu, jangan keduanya.
}
