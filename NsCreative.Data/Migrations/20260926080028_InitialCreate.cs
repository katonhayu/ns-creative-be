using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NsCreative.Data.Migrations
{
    /// <summary>
    /// Baseline migration (database-first). Ke-14 tabel sudah dibuat lewat
    /// schema-nscreative.sql, jadi migration ini sengaja kosong: ia hanya menandai
    /// titik awal di __EFMigrationsHistory. Snapshot model di
    /// AppDbContextModelSnapshot.cs tetap lengkap, sehingga migration berikutnya
    /// hanya berisi perubahan sesudah baseline ini.
    /// </summary>
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
