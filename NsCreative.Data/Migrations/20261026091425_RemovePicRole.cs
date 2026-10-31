using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NsCreative.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemovePicRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_events_pic",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "fk_schedules_pic",
                table: "schedules");

            migrationBuilder.DropForeignKey(
                name: "fk_tasks_pic",
                table: "tasks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_role",
                table: "users");

            migrationBuilder.RenameColumn(
                name: "pic_user_id",
                table: "tasks",
                newName: "pic_crew_id");

            migrationBuilder.RenameIndex(
                name: "ix_tasks_pic_user_id",
                table: "tasks",
                newName: "ix_tasks_pic_crew_id");

            migrationBuilder.RenameColumn(
                name: "pic_user_id",
                table: "schedules",
                newName: "pic_crew_id");

            migrationBuilder.RenameIndex(
                name: "ix_schedules_pic_user_id",
                table: "schedules",
                newName: "ix_schedules_pic_crew_id");

            migrationBuilder.RenameColumn(
                name: "pic_user_id",
                table: "events",
                newName: "pic_crew_id");

            migrationBuilder.RenameIndex(
                name: "ix_events_pic_user_id",
                table: "events",
                newName: "ix_events_pic_crew_id");

            // Sampai di sini kolom pic_crew_id masih berisi id user. Langkah data:
            // 1. Setiap akun berperan pic, dan setiap akun yang memegang event, harus punya data crew.
            //    Crew yang belum bertaut dengan nama sama persis (dan satu-satunya) ditautkan;
            //    selain itu dibuat crew baru.
            migrationBuilder.Sql("""
                UPDATE crews c
                SET user_id = u.id
                FROM users u
                WHERE c.user_id IS NULL
                  AND c.deleted_at IS NULL
                  AND lower(trim(c.nama)) = lower(trim(u.nama))
                  AND (u.role = 'pic' OR u.id IN (SELECT pic_crew_id FROM events))
                  AND NOT EXISTS (SELECT 1 FROM crews x WHERE x.user_id = u.id)
                  AND (SELECT count(*) FROM crews y
                       WHERE y.user_id IS NULL AND y.deleted_at IS NULL
                         AND lower(trim(y.nama)) = lower(trim(u.nama))) = 1
                  AND (SELECT count(*) FROM users z WHERE lower(trim(z.nama)) = lower(trim(u.nama))) = 1;

                INSERT INTO crews (id, user_id, nama)
                SELECT gen_random_uuid(), u.id, u.nama
                FROM users u
                WHERE (u.role = 'pic' OR u.id IN (SELECT pic_crew_id FROM events))
                  AND NOT EXISTS (SELECT 1 FROM crews c WHERE c.user_id = u.id);
                """);

            // 2. Ganti id user dengan id crew yang ditautkan ke akun tersebut. PIC task/sesi yang
            //    akunnya tidak punya data crew (misalnya admin) dikosongkan.
            migrationBuilder.Sql("""
                UPDATE events e
                SET pic_crew_id = (SELECT c.id FROM crews c WHERE c.user_id = e.pic_crew_id
                                   ORDER BY c.deleted_at NULLS FIRST, c.created_at LIMIT 1);

                UPDATE tasks t
                SET pic_crew_id = (SELECT c.id FROM crews c WHERE c.user_id = t.pic_crew_id
                                   ORDER BY c.deleted_at NULLS FIRST, c.created_at LIMIT 1)
                WHERE t.pic_crew_id IS NOT NULL;

                UPDATE schedules s
                SET pic_crew_id = (SELECT c.id FROM crews c WHERE c.user_id = s.pic_crew_id
                                   ORDER BY c.deleted_at NULLS FIRST, c.created_at LIMIT 1)
                WHERE s.pic_crew_id IS NOT NULL;
                """);

            // 3. Peran pic dihapus. Akunnya menjadi crew; hak kelola event mengikuti PIC event.
            migrationBuilder.Sql("UPDATE users SET role = 'crew' WHERE role = 'pic';");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_role",
                table: "users",
                sql: "role IN ('admin', 'event_manager', 'crew')");

            migrationBuilder.AddForeignKey(
                name: "fk_events_pic",
                table: "events",
                column: "pic_crew_id",
                principalTable: "crews",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_schedules_pic",
                table: "schedules",
                column: "pic_crew_id",
                principalTable: "crews",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_tasks_pic",
                table: "tasks",
                column: "pic_crew_id",
                principalTable: "crews",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_events_pic",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "fk_schedules_pic",
                table: "schedules");

            migrationBuilder.DropForeignKey(
                name: "fk_tasks_pic",
                table: "tasks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_role",
                table: "users");

            migrationBuilder.RenameColumn(
                name: "pic_crew_id",
                table: "tasks",
                newName: "pic_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_tasks_pic_crew_id",
                table: "tasks",
                newName: "ix_tasks_pic_user_id");

            migrationBuilder.RenameColumn(
                name: "pic_crew_id",
                table: "schedules",
                newName: "pic_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_schedules_pic_crew_id",
                table: "schedules",
                newName: "ix_schedules_pic_user_id");

            migrationBuilder.RenameColumn(
                name: "pic_crew_id",
                table: "events",
                newName: "pic_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_events_pic_crew_id",
                table: "events",
                newName: "ix_events_pic_user_id");

            // Kebalikan langkah data (tidak sempurna): PIC kembali ke akun yang ditautkan ke crew-nya.
            // Event yang crew PIC-nya tidak punya akun membuat Down gagal; tautkan akunnya dulu.
            // Pemegang event dan event manager menjadi pic, karena peran crew lama tidak boleh menjadi PIC
            // dan peran event_manager belum ada. Data crew yang dibuat Up tidak dihapus.
            migrationBuilder.Sql("""
                UPDATE users SET role = 'pic'
                WHERE role = 'event_manager'
                   OR id IN (SELECT c.user_id FROM events e JOIN crews c ON c.id = e.pic_user_id);

                UPDATE events e SET pic_user_id = (SELECT c.user_id FROM crews c WHERE c.id = e.pic_user_id);
                UPDATE tasks t SET pic_user_id = (SELECT c.user_id FROM crews c WHERE c.id = t.pic_user_id)
                WHERE t.pic_user_id IS NOT NULL;
                UPDATE schedules s SET pic_user_id = (SELECT c.user_id FROM crews c WHERE c.id = s.pic_user_id)
                WHERE s.pic_user_id IS NOT NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_role",
                table: "users",
                sql: "role IN ('admin', 'pic', 'crew')");

            migrationBuilder.AddForeignKey(
                name: "fk_events_pic",
                table: "events",
                column: "pic_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_schedules_pic",
                table: "schedules",
                column: "pic_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_tasks_pic",
                table: "tasks",
                column: "pic_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
