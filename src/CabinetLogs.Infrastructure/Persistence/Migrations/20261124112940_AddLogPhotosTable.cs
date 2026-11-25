using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CabinetLogs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLogPhotosTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "log_photos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    log_id = table.Column<Guid>(type: "uuid", nullable: false),
                    photo_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_log_photos", x => x.id);
                    table.ForeignKey(
                        name: "fk_log_photos_logs_log_id",
                        column: x => x.log_id,
                        principalTable: "logs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_log_photos_log_id_sort_order",
                table: "log_photos",
                columns: new[] { "log_id", "sort_order" });

            migrationBuilder.Sql("""
                INSERT INTO public.log_photos (id, log_id, photo_path, sort_order, created_at)
                SELECT gen_random_uuid(), id, photo_path, 0, created_at
                FROM public.logs
                WHERE photo_path IS NOT NULL AND photo_path <> '';

                ALTER TABLE public.log_photos ENABLE ROW LEVEL SECURITY;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "log_photos");
        }
    }
}
