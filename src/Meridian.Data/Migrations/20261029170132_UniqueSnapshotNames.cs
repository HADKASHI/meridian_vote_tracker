using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Meridian.Data.Migrations
{
    /// <summary>
    /// Every snapshot holds its name, failed ones included, ignoring case and surrounding
    /// spaces. An expression index, so it is SQL here and not part of the EF model; it
    /// also decides two simultaneous creates with one name.
    /// </summary>
    public partial class UniqueSnapshotNames : Migration
    {
        public const string IndexName = "ux_snapshot_name";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"CREATE UNIQUE INDEX {IndexName} ON snapshot (lower(btrim(name)));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"DROP INDEX {IndexName};");
        }
    }
}
