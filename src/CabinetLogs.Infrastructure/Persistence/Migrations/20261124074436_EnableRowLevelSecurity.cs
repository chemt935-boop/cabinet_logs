using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CabinetLogs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE public.cabinets ENABLE ROW LEVEL SECURITY;
                ALTER TABLE public.client_devices ENABLE ROW LEVEL SECURITY;
                ALTER TABLE public.logs ENABLE ROW LEVEL SECURITY;
                ALTER TABLE public.pending_storage_cleanups ENABLE ROW LEVEL SECURITY;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE public.cabinets DISABLE ROW LEVEL SECURITY;
                ALTER TABLE public.client_devices DISABLE ROW LEVEL SECURITY;
                ALTER TABLE public.logs DISABLE ROW LEVEL SECURITY;
                ALTER TABLE public.pending_storage_cleanups DISABLE ROW LEVEL SECURITY;
                """);
        }
    }
}
