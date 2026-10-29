using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Meridian.Data.Migrations
{
    /// <inheritdoc />
    public partial class PdfExtraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_job_kind",
                table: "job");

            migrationBuilder.DropCheckConstraint(
                name: "ck_job_link",
                table: "job");

            migrationBuilder.AddColumn<long>(
                name: "extraction_id",
                table: "job",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "extraction",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('history_seq')"),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    blob_name = table.Column<string>(type: "text", nullable: false),
                    sha256 = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_by = table.Column<string>(type: "text", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    status = table.Column<string>(type: "text", nullable: false),
                    extracted = table.Column<string>(type: "jsonb", nullable: true),
                    sheet = table.Column<string>(type: "jsonb", nullable: true),
                    check_result = table.Column<string>(type: "jsonb", nullable: true),
                    edits = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    error = table.Column<string>(type: "text", nullable: true),
                    decided_by = table.Column<string>(type: "text", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_comment = table.Column<string>(type: "text", nullable: true),
                    import_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_extraction", x => x.id);
                    table.CheckConstraint("ck_extraction_decision", "(status IN ('approved', 'rejected')) = (decided_by IS NOT NULL)");
                    table.CheckConstraint("ck_extraction_import", "(status = 'approved') = (import_id IS NOT NULL)");
                    table.CheckConstraint("ck_extraction_status", "status IN ('extracting', 'needs_review', 'failed', 'approved', 'rejected')");
                    table.ForeignKey(
                        name: "fk_extraction_import_import_id",
                        column: x => x.import_id,
                        principalTable: "import",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_job_extraction_id",
                table: "job",
                column: "extraction_id",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_job_kind",
                table: "job",
                sql: "kind IN ('import', 'snapshot', 'pdf_extraction')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_job_link",
                table: "job",
                sql: "(kind = 'import') = (import_id IS NOT NULL) AND (kind = 'snapshot') = (snapshot_id IS NOT NULL) AND (kind = 'pdf_extraction') = (extraction_id IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_extraction_status",
                table: "extraction",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_extraction_import",
                table: "extraction",
                column: "import_id",
                unique: true,
                filter: "import_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_extraction_sha256",
                table: "extraction",
                column: "sha256",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_job_extractions_extraction_id",
                table: "job",
                column: "extraction_id",
                principalTable: "extraction",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_job_extractions_extraction_id",
                table: "job");

            migrationBuilder.DropTable(
                name: "extraction");

            migrationBuilder.DropIndex(
                name: "ix_job_extraction_id",
                table: "job");

            migrationBuilder.DropCheckConstraint(
                name: "ck_job_kind",
                table: "job");

            migrationBuilder.DropCheckConstraint(
                name: "ck_job_link",
                table: "job");

            migrationBuilder.DropColumn(
                name: "extraction_id",
                table: "job");

            migrationBuilder.AddCheckConstraint(
                name: "ck_job_kind",
                table: "job",
                sql: "kind IN ('import', 'snapshot')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_job_link",
                table: "job",
                sql: "(kind = 'import') = (import_id IS NOT NULL) AND (kind = 'snapshot') = (snapshot_id IS NOT NULL)");
        }
    }
}
