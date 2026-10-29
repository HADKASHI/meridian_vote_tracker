using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Meridian.Data.Migrations
{
    /// <summary>
    /// Every snapshot holds its name, failed ones included, ignoring case and surrounding
    /// spaces. An expression index, so it is SQL here and not part of the EF model; it
    /// also decides two simultaneous creates with one name.
    /// Snapshots are insert-only, so names already shared when this runs cannot be
    /// renamed: the index leaves those rows out (by id), and the create endpoint still
    /// refuses their names.
    /// </summary>
    public partial class UniqueSnapshotNames : Migration
    {
        public const string IndexName = "ux_snapshot_name";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($$"""
                DO $$
                DECLARE shared text;
                BEGIN
                  SELECT string_agg(id::text, ', ' ORDER BY id) INTO shared
                  FROM (SELECT id, count(*) OVER (PARTITION BY lower(btrim(name))) AS n FROM snapshot) s
                  WHERE n > 1;
                  EXECUTE 'CREATE UNIQUE INDEX {{IndexName}} ON snapshot (lower(btrim(name)))'
                    || CASE WHEN shared IS NULL THEN '' ELSE ' WHERE id NOT IN (' || shared || ')' END;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"DROP INDEX {IndexName};");
        }
    }
}
