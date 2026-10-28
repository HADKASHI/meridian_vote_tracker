using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Meridian.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "history_seq");

            migrationBuilder.CreateTable(
                name: "app_user",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_user", x => x.id);
                    table.CheckConstraint("ck_app_user_role", "role IN ('analyst', 'admin')");
                });

            migrationBuilder.CreateTable(
                name: "member_state",
                columns: table => new
                {
                    code = table.Column<string>(type: "char(3)", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_member_state", x => x.code);
                    table.CheckConstraint("ck_member_state_code", "code ~ '^[A-Z]{3}$'");
                });

            migrationBuilder.CreateTable(
                name: "import",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('history_seq')"),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    blob_name = table.Column<string>(type: "text", nullable: false),
                    sha256 = table.Column<string>(type: "char(64)", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_by = table.Column<string>(type: "text", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import", x => x.id);
                    table.ForeignKey(
                        name: "fk_import_app_user_uploaded_by",
                        column: x => x.uploaded_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "snapshot",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('history_seq')"),
                    name = table.Column<string>(type: "text", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    selection = table.Column<string>(type: "jsonb", nullable: false),
                    scoring_rules_version = table.Column<string>(type: "text", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_snapshot", x => x.id);
                    table.ForeignKey(
                        name: "fk_snapshot_app_user_created_by",
                        column: x => x.created_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "membership_period",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('history_seq')"),
                    state_code = table.Column<string>(type: "char(3)", nullable: false),
                    member_from = table.Column<DateOnly>(type: "date", nullable: false),
                    member_to = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_membership_period", x => x.id);
                    table.CheckConstraint("ck_membership_period_range", "member_to >= member_from");
                    table.ForeignKey(
                        name: "fk_membership_period_member_state_state_code",
                        column: x => x.state_code,
                        principalTable: "member_state",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "import_parse",
                columns: table => new
                {
                    import_id = table.Column<long>(type: "bigint", nullable: false),
                    header = table.Column<string>(type: "jsonb", nullable: false),
                    has_bom = table.Column<bool>(type: "boolean", nullable: false),
                    line_endings = table.Column<string>(type: "text", nullable: false),
                    data_rows = table.Column<int>(type: "integer", nullable: false),
                    max_published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejected_reason = table.Column<string>(type: "text", nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_parse", x => x.import_id);
                    table.ForeignKey(
                        name: "fk_import_parse_import_import_id",
                        column: x => x.import_id,
                        principalTable: "import",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vote",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('history_seq')"),
                    assembly_vote_id = table.Column<string>(type: "text", nullable: false),
                    first_seen_import_id = table.Column<long>(type: "bigint", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vote", x => x.id);
                    table.ForeignKey(
                        name: "fk_vote_import_first_seen_import_id",
                        column: x => x.first_seen_import_id,
                        principalTable: "import",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    kind = table.Column<string>(type: "text", nullable: false),
                    import_id = table.Column<long>(type: "bigint", nullable: true),
                    snapshot_id = table.Column<long>(type: "bigint", nullable: true),
                    state = table.Column<string>(type: "text", nullable: false),
                    rows_read = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    votes_done = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    votes_total = table.Column<int>(type: "integer", nullable: true),
                    heartbeat_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    trace_parent = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job", x => x.id);
                    table.CheckConstraint("ck_job_kind", "kind IN ('import', 'snapshot')");
                    table.CheckConstraint("ck_job_link", "(kind = 'import') = (import_id IS NOT NULL) AND (kind = 'snapshot') = (snapshot_id IS NOT NULL)");
                    table.CheckConstraint("ck_job_state", "state IN ('queued', 'running', 'done', 'failed')");
                    table.ForeignKey(
                        name: "fk_job_import_import_id",
                        column: x => x.import_id,
                        principalTable: "import",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_job_snapshot_snapshot_id",
                        column: x => x.snapshot_id,
                        principalTable: "snapshot",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "publication",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('history_seq')"),
                    snapshot_id = table.Column<long>(type: "bigint", nullable: false),
                    quarter = table.Column<string>(type: "text", nullable: false),
                    published_by = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    supersedes_publication_id = table.Column<long>(type: "bigint", nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_publication", x => x.id);
                    table.UniqueConstraint("ak_publication_id_quarter", x => new { x.id, x.quarter });
                    table.CheckConstraint("ck_publication_quarter", "quarter ~ '^[0-9]{4}-Q[1-4]$'");
                    table.CheckConstraint("ck_publication_reason", "(supersedes_publication_id IS NULL) = (reason IS NULL)");
                    table.ForeignKey(
                        name: "fk_publication_app_user_published_by",
                        column: x => x.published_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_publication_publication_supersedes_publication_id_quarter",
                        columns: x => new { x.supersedes_publication_id, x.quarter },
                        principalTable: "publication",
                        principalColumns: new[] { "id", "quarter" });
                    table.ForeignKey(
                        name: "fk_publication_snapshot_snapshot_id",
                        column: x => x.snapshot_id,
                        principalTable: "snapshot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "snapshot_result",
                columns: table => new
                {
                    snapshot_id = table.Column<long>(type: "bigint", nullable: false),
                    state_code = table.Column<string>(type: "char(3)", nullable: false),
                    matched_weight = table.Column<int>(type: "integer", nullable: false),
                    counted_weight = table.Column<int>(type: "integer", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_snapshot_result", x => new { x.snapshot_id, x.state_code });
                    table.CheckConstraint("ck_snapshot_result_rank", "(rank IS NULL) = (counted_weight = 0)");
                    table.CheckConstraint("ck_snapshot_result_weights", "0 <= matched_weight AND matched_weight <= counted_weight");
                    table.ForeignKey(
                        name: "fk_snapshot_result_member_state_state_code",
                        column: x => x.state_code,
                        principalTable: "member_state",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_snapshot_result_snapshot_snapshot_id",
                        column: x => x.snapshot_id,
                        principalTable: "snapshot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "import_row",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('history_seq')"),
                    import_id = table.Column<long>(type: "bigint", nullable: false),
                    line_no = table.Column<int>(type: "integer", nullable: false),
                    raw_text = table.Column<string>(type: "text", nullable: false),
                    vote_id = table.Column<long>(type: "bigint", nullable: true),
                    state_code = table.Column<string>(type: "text", nullable: true),
                    country_name = table.Column<string>(type: "text", nullable: true),
                    ballot = table.Column<string>(type: "text", nullable: true),
                    session = table.Column<int>(type: "integer", nullable: true),
                    vote_date = table.Column<DateOnly>(type: "date", nullable: true),
                    title = table.Column<string>(type: "text", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    outcome = table.Column<string>(type: "text", nullable: false),
                    reason_code = table.Column<string>(type: "text", nullable: true),
                    reason_message = table.Column<string>(type: "text", nullable: true),
                    normalisations = table.Column<string>(type: "jsonb", nullable: true),
                    country_vote_revision_id = table.Column<long>(type: "bigint", nullable: true),
                    details_revision_id = table.Column<long>(type: "bigint", nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_row", x => x.id);
                    table.UniqueConstraint("ak_import_row_import_id_line_no", x => new { x.import_id, x.line_no });
                    table.CheckConstraint("ck_import_row_outcome", "outcome IN ('new', 'unchanged', 'duplicate', 'invalid', 'contradiction', 'conflict')");
                    table.ForeignKey(
                        name: "fk_import_row_import_import_id",
                        column: x => x.import_id,
                        principalTable: "import",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_import_row_votes_vote_id",
                        column: x => x.vote_id,
                        principalTable: "vote",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "import_warning",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('history_seq')"),
                    import_id = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    vote_id = table.Column<long>(type: "bigint", nullable: true),
                    detail = table.Column<string>(type: "jsonb", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_warning", x => x.id);
                    table.ForeignKey(
                        name: "fk_import_warning_import_import_id",
                        column: x => x.import_id,
                        principalTable: "import",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_import_warning_votes_vote_id",
                        column: x => x.vote_id,
                        principalTable: "vote",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "position_revision",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('history_seq')"),
                    vote_id = table.Column<long>(type: "bigint", nullable: false),
                    stance = table.Column<string>(type: "text", nullable: false),
                    weight = table.Column<short>(type: "smallint", nullable: false),
                    set_by = table.Column<string>(type: "text", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_position_revision", x => x.id);
                    table.UniqueConstraint("ak_position_revision_id_vote_id", x => new { x.id, x.vote_id });
                    table.CheckConstraint("ck_position_revision_stance", "stance IN ('yes', 'no')");
                    table.CheckConstraint("ck_position_revision_weight", "weight BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_position_revision_app_user_set_by",
                        column: x => x.set_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_position_revision_vote_vote_id",
                        column: x => x.vote_id,
                        principalTable: "vote",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "snapshot_warning",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('history_seq')"),
                    snapshot_id = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    vote_id = table.Column<long>(type: "bigint", nullable: true),
                    detail = table.Column<string>(type: "jsonb", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_snapshot_warning", x => x.id);
                    table.ForeignKey(
                        name: "fk_snapshot_warning_snapshot_snapshot_id",
                        column: x => x.snapshot_id,
                        principalTable: "snapshot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_snapshot_warning_vote_vote_id",
                        column: x => x.vote_id,
                        principalTable: "vote",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "conflict",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('history_seq')"),
                    vote_id = table.Column<long>(type: "bigint", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    opened_by_import_id = table.Column<long>(type: "bigint", nullable: false),
                    state_code = table.Column<string>(type: "char(3)", nullable: true),
                    incoming_ballot = table.Column<string>(type: "char(1)", nullable: true),
                    compared_country_vote_revision_id = table.Column<long>(type: "bigint", nullable: true),
                    incoming_session = table.Column<int>(type: "integer", nullable: true),
                    incoming_vote_date = table.Column<DateOnly>(type: "date", nullable: true),
                    incoming_title = table.Column<string>(type: "text", nullable: true),
                    incoming_title_key = table.Column<string>(type: "text", nullable: true),
                    compared_details_revision_id = table.Column<long>(type: "bigint", nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conflict", x => x.id);
                    table.CheckConstraint("ck_conflict_incoming_ballot", "incoming_ballot IN ('Y', 'N', 'A', 'X')");
                    table.CheckConstraint("ck_conflict_subject", "subject IN ('ballot', 'details')");
                    table.CheckConstraint("ck_conflict_subject_columns", "(subject = 'ballot'\n  AND num_nonnulls(state_code, incoming_ballot, compared_country_vote_revision_id) = 3\n  AND num_nonnulls(incoming_session, incoming_vote_date, incoming_title, incoming_title_key, compared_details_revision_id) = 0)\nOR (subject = 'details'\n  AND num_nonnulls(state_code, incoming_ballot, compared_country_vote_revision_id) = 0\n  AND num_nonnulls(incoming_session, incoming_vote_date, incoming_title, incoming_title_key, compared_details_revision_id) = 5)");
                    table.ForeignKey(
                        name: "fk_conflict_import_opened_by_import_id",
                        column: x => x.opened_by_import_id,
                        principalTable: "import",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_conflict_vote_vote_id",
                        column: x => x.vote_id,
                        principalTable: "vote",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "decision_attempt_not_applied",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('history_seq')"),
                    conflict_id = table.Column<long>(type: "bigint", nullable: false),
                    attempted_by = table.Column<string>(type: "text", nullable: false),
                    attempted = table.Column<string>(type: "text", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_decision_attempt_not_applied", x => x.id);
                    table.CheckConstraint("ck_decision_attempt_not_applied_attempted", "attempted IN ('accept', 'reject')");
                    table.ForeignKey(
                        name: "fk_decision_attempt_not_applied_app_user_attempted_by",
                        column: x => x.attempted_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_decision_attempt_not_applied_conflict_conflict_id",
                        column: x => x.conflict_id,
                        principalTable: "conflict",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "import_row_conflict",
                columns: table => new
                {
                    import_row_id = table.Column<long>(type: "bigint", nullable: false),
                    conflict_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_row_conflict", x => new { x.import_row_id, x.conflict_id });
                    table.ForeignKey(
                        name: "fk_import_row_conflict_conflict_conflict_id",
                        column: x => x.conflict_id,
                        principalTable: "conflict",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_import_row_conflict_import_row_import_row_id",
                        column: x => x.import_row_id,
                        principalTable: "import_row",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "conflict_decision",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('history_seq')"),
                    conflict_id = table.Column<long>(type: "bigint", nullable: false),
                    decision = table.Column<string>(type: "text", nullable: false),
                    decided_by = table.Column<string>(type: "text", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    replaced_country_vote_revision_id = table.Column<long>(type: "bigint", nullable: true),
                    replaced_details_revision_id = table.Column<long>(type: "bigint", nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conflict_decision", x => x.id);
                    table.CheckConstraint("ck_conflict_decision_decision", "decision IN ('accept', 'reject')");
                    table.ForeignKey(
                        name: "fk_conflict_decision_app_user_decided_by",
                        column: x => x.decided_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_conflict_decision_conflict_conflict_id",
                        column: x => x.conflict_id,
                        principalTable: "conflict",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "country_vote_revision",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('history_seq')"),
                    vote_id = table.Column<long>(type: "bigint", nullable: false),
                    state_code = table.Column<string>(type: "char(3)", nullable: false),
                    ballot = table.Column<string>(type: "char(1)", nullable: false),
                    origin_import_row_id = table.Column<long>(type: "bigint", nullable: true),
                    origin_decision_id = table.Column<long>(type: "bigint", nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_country_vote_revision", x => x.id);
                    table.UniqueConstraint("ak_country_vote_revision_id_vote_id_state_code", x => new { x.id, x.vote_id, x.state_code });
                    table.CheckConstraint("ck_country_vote_revision_ballot", "ballot IN ('Y', 'N', 'A', 'X')");
                    table.CheckConstraint("ck_country_vote_revision_one_origin", "num_nonnulls(origin_import_row_id, origin_decision_id) = 1");
                    table.ForeignKey(
                        name: "fk_country_vote_revision_conflict_decisions_origin_decision_id",
                        column: x => x.origin_decision_id,
                        principalTable: "conflict_decision",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_country_vote_revision_import_row_origin_import_row_id",
                        column: x => x.origin_import_row_id,
                        principalTable: "import_row",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_country_vote_revision_member_state_state_code",
                        column: x => x.state_code,
                        principalTable: "member_state",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_country_vote_revision_vote_vote_id",
                        column: x => x.vote_id,
                        principalTable: "vote",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vote_details_revision",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('history_seq')"),
                    vote_id = table.Column<long>(type: "bigint", nullable: false),
                    session = table.Column<int>(type: "integer", nullable: false),
                    vote_date = table.Column<DateOnly>(type: "date", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    origin_import_row_id = table.Column<long>(type: "bigint", nullable: true),
                    origin_decision_id = table.Column<long>(type: "bigint", nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vote_details_revision", x => x.id);
                    table.UniqueConstraint("ak_vote_details_revision_id_vote_id", x => new { x.id, x.vote_id });
                    table.CheckConstraint("ck_vote_details_revision_one_origin", "num_nonnulls(origin_import_row_id, origin_decision_id) = 1");
                    table.CheckConstraint("ck_vote_details_revision_session", "session > 0");
                    table.ForeignKey(
                        name: "fk_vote_details_revision_conflict_decisions_origin_decision_id",
                        column: x => x.origin_decision_id,
                        principalTable: "conflict_decision",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_vote_details_revision_import_row_origin_import_row_id",
                        column: x => x.origin_import_row_id,
                        principalTable: "import_row",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_vote_details_revision_vote_vote_id",
                        column: x => x.vote_id,
                        principalTable: "vote",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "country_vote_current",
                columns: table => new
                {
                    vote_id = table.Column<long>(type: "bigint", nullable: false),
                    state_code = table.Column<string>(type: "char(3)", nullable: false),
                    revision_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_country_vote_current", x => new { x.vote_id, x.state_code });
                    table.ForeignKey(
                        name: "fk_country_vote_current_country_vote_revision_revision_id_vote",
                        columns: x => new { x.revision_id, x.vote_id, x.state_code },
                        principalTable: "country_vote_revision",
                        principalColumns: new[] { "id", "vote_id", "state_code" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "snapshot_vote",
                columns: table => new
                {
                    snapshot_id = table.Column<long>(type: "bigint", nullable: false),
                    vote_id = table.Column<long>(type: "bigint", nullable: false),
                    details_revision_id = table.Column<long>(type: "bigint", nullable: false),
                    position_revision_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_snapshot_vote", x => new { x.snapshot_id, x.vote_id });
                    table.ForeignKey(
                        name: "fk_snapshot_vote_position_revision_position_revision_id_vote_id",
                        columns: x => new { x.position_revision_id, x.vote_id },
                        principalTable: "position_revision",
                        principalColumns: new[] { "id", "vote_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_snapshot_vote_snapshot_snapshot_id",
                        column: x => x.snapshot_id,
                        principalTable: "snapshot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_snapshot_vote_vote_details_revision_details_revision_id_vot",
                        columns: x => new { x.details_revision_id, x.vote_id },
                        principalTable: "vote_details_revision",
                        principalColumns: new[] { "id", "vote_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_snapshot_vote_vote_vote_id",
                        column: x => x.vote_id,
                        principalTable: "vote",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vote_current",
                columns: table => new
                {
                    vote_id = table.Column<long>(type: "bigint", nullable: false),
                    details_revision_id = table.Column<long>(type: "bigint", nullable: false),
                    position_revision_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vote_current", x => x.vote_id);
                    table.ForeignKey(
                        name: "fk_vote_current_position_revision_position_revision_id_vote_id",
                        columns: x => new { x.position_revision_id, x.vote_id },
                        principalTable: "position_revision",
                        principalColumns: new[] { "id", "vote_id" });
                    table.ForeignKey(
                        name: "fk_vote_current_vote_details_revision_details_revision_id_vote",
                        columns: x => new { x.details_revision_id, x.vote_id },
                        principalTable: "vote_details_revision",
                        principalColumns: new[] { "id", "vote_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vote_current_vote_vote_id",
                        column: x => x.vote_id,
                        principalTable: "vote",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "snapshot_line",
                columns: table => new
                {
                    snapshot_id = table.Column<long>(type: "bigint", nullable: false),
                    vote_id = table.Column<long>(type: "bigint", nullable: false),
                    state_code = table.Column<string>(type: "char(3)", nullable: false),
                    country_vote_revision_id = table.Column<long>(type: "bigint", nullable: true),
                    category = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_snapshot_line", x => new { x.snapshot_id, x.vote_id, x.state_code });
                    table.CheckConstraint("ck_snapshot_line_category", "category IN ('match', 'mismatch', 'abstain', 'absent', 'non_member', 'no_data')");
                    table.ForeignKey(
                        name: "fk_snapshot_line_country_vote_revision_country_vote_revision_i",
                        columns: x => new { x.country_vote_revision_id, x.vote_id, x.state_code },
                        principalTable: "country_vote_revision",
                        principalColumns: new[] { "id", "vote_id", "state_code" });
                    table.ForeignKey(
                        name: "fk_snapshot_line_member_state_state_code",
                        column: x => x.state_code,
                        principalTable: "member_state",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_snapshot_line_snapshot_vote_snapshot_id_vote_id",
                        columns: x => new { x.snapshot_id, x.vote_id },
                        principalTable: "snapshot_vote",
                        principalColumns: new[] { "snapshot_id", "vote_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_conflict_compared_country_vote_revision_id_vote_id_state_co",
                table: "conflict",
                columns: new[] { "compared_country_vote_revision_id", "vote_id", "state_code" });

            migrationBuilder.CreateIndex(
                name: "ix_conflict_compared_details_revision_id_vote_id",
                table: "conflict",
                columns: new[] { "compared_details_revision_id", "vote_id" });

            migrationBuilder.CreateIndex(
                name: "ix_conflict_opened_by_import_id",
                table: "conflict",
                column: "opened_by_import_id");

            migrationBuilder.CreateIndex(
                name: "ix_conflict_vote_id",
                table: "conflict",
                column: "vote_id");

            migrationBuilder.CreateIndex(
                name: "ux_conflict_ballot_incoming",
                table: "conflict",
                columns: new[] { "compared_country_vote_revision_id", "incoming_ballot" },
                unique: true,
                filter: "subject = 'ballot'");

            migrationBuilder.CreateIndex(
                name: "ux_conflict_details_incoming",
                table: "conflict",
                columns: new[] { "compared_details_revision_id", "incoming_session", "incoming_vote_date", "incoming_title_key" },
                unique: true,
                filter: "subject = 'details'");

            migrationBuilder.CreateIndex(
                name: "ix_conflict_decision_conflict_id",
                table: "conflict_decision",
                column: "conflict_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_conflict_decision_decided_by",
                table: "conflict_decision",
                column: "decided_by");

            migrationBuilder.CreateIndex(
                name: "ix_conflict_decision_replaced_country_vote_revision_id",
                table: "conflict_decision",
                column: "replaced_country_vote_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_conflict_decision_replaced_details_revision_id",
                table: "conflict_decision",
                column: "replaced_details_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_country_vote_current_revision_id_vote_id_state_code",
                table: "country_vote_current",
                columns: new[] { "revision_id", "vote_id", "state_code" });

            migrationBuilder.CreateIndex(
                name: "ix_country_vote_current_state_code",
                table: "country_vote_current",
                column: "state_code");

            migrationBuilder.CreateIndex(
                name: "ix_country_vote_revision_origin_decision_id",
                table: "country_vote_revision",
                column: "origin_decision_id");

            migrationBuilder.CreateIndex(
                name: "ix_country_vote_revision_origin_import_row_id",
                table: "country_vote_revision",
                column: "origin_import_row_id");

            migrationBuilder.CreateIndex(
                name: "ix_country_vote_revision_state_code",
                table: "country_vote_revision",
                column: "state_code");

            migrationBuilder.CreateIndex(
                name: "ix_country_vote_revision_vote_id_state_code_id",
                table: "country_vote_revision",
                columns: new[] { "vote_id", "state_code", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_decision_attempt_not_applied_attempted_by",
                table: "decision_attempt_not_applied",
                column: "attempted_by");

            migrationBuilder.CreateIndex(
                name: "ix_decision_attempt_not_applied_conflict_id",
                table: "decision_attempt_not_applied",
                column: "conflict_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_sha256",
                table: "import",
                column: "sha256");

            migrationBuilder.CreateIndex(
                name: "ix_import_uploaded_by",
                table: "import",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "ix_import_row_import_id_outcome",
                table: "import_row",
                columns: new[] { "import_id", "outcome" });

            migrationBuilder.CreateIndex(
                name: "ix_import_row_import_id_vote_id",
                table: "import_row",
                columns: new[] { "import_id", "vote_id" });

            migrationBuilder.CreateIndex(
                name: "ix_import_row_vote_id",
                table: "import_row",
                column: "vote_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_row_conflict_conflict_id",
                table: "import_row_conflict",
                column: "conflict_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_warning_import_id",
                table: "import_warning",
                column: "import_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_warning_vote_id",
                table: "import_warning",
                column: "vote_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_import_id",
                table: "job",
                column: "import_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_job_queued",
                table: "job",
                columns: new[] { "kind", "id" },
                filter: "state = 'queued'");

            migrationBuilder.CreateIndex(
                name: "ix_job_running_heartbeat",
                table: "job",
                column: "heartbeat_at",
                filter: "state = 'running'");

            migrationBuilder.CreateIndex(
                name: "ix_job_snapshot_id",
                table: "job",
                column: "snapshot_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_membership_period_state_code",
                table: "membership_period",
                column: "state_code");

            migrationBuilder.CreateIndex(
                name: "ix_position_revision_set_by",
                table: "position_revision",
                column: "set_by");

            migrationBuilder.CreateIndex(
                name: "ix_position_revision_vote_id_id",
                table: "position_revision",
                columns: new[] { "vote_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_publication_published_by",
                table: "publication",
                column: "published_by");

            migrationBuilder.CreateIndex(
                name: "ix_publication_snapshot_id",
                table: "publication",
                column: "snapshot_id");

            migrationBuilder.CreateIndex(
                name: "ix_publication_supersedes_publication_id_quarter",
                table: "publication",
                columns: new[] { "supersedes_publication_id", "quarter" });

            migrationBuilder.CreateIndex(
                name: "ux_publication_first_per_quarter",
                table: "publication",
                column: "quarter",
                unique: true,
                filter: "supersedes_publication_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_publication_one_successor",
                table: "publication",
                column: "supersedes_publication_id",
                unique: true,
                filter: "supersedes_publication_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_snapshot_created_by",
                table: "snapshot",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_snapshot_line_country_vote_revision_id_vote_id_state_code",
                table: "snapshot_line",
                columns: new[] { "country_vote_revision_id", "vote_id", "state_code" });

            migrationBuilder.CreateIndex(
                name: "ix_snapshot_line_snapshot_id_state_code",
                table: "snapshot_line",
                columns: new[] { "snapshot_id", "state_code" });

            migrationBuilder.CreateIndex(
                name: "ix_snapshot_line_state_code",
                table: "snapshot_line",
                column: "state_code");

            migrationBuilder.CreateIndex(
                name: "ix_snapshot_result_state_code",
                table: "snapshot_result",
                column: "state_code");

            migrationBuilder.CreateIndex(
                name: "ix_snapshot_vote_details_revision_id_vote_id",
                table: "snapshot_vote",
                columns: new[] { "details_revision_id", "vote_id" });

            migrationBuilder.CreateIndex(
                name: "ix_snapshot_vote_position_revision_id_vote_id",
                table: "snapshot_vote",
                columns: new[] { "position_revision_id", "vote_id" });

            migrationBuilder.CreateIndex(
                name: "ix_snapshot_vote_vote_id",
                table: "snapshot_vote",
                column: "vote_id");

            migrationBuilder.CreateIndex(
                name: "ix_snapshot_warning_snapshot_id",
                table: "snapshot_warning",
                column: "snapshot_id");

            migrationBuilder.CreateIndex(
                name: "ix_snapshot_warning_vote_id",
                table: "snapshot_warning",
                column: "vote_id");

            migrationBuilder.CreateIndex(
                name: "ix_vote_assembly_vote_id",
                table: "vote",
                column: "assembly_vote_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vote_first_seen_import_id",
                table: "vote",
                column: "first_seen_import_id");

            migrationBuilder.CreateIndex(
                name: "ix_vote_current_details_revision_id_vote_id",
                table: "vote_current",
                columns: new[] { "details_revision_id", "vote_id" });

            migrationBuilder.CreateIndex(
                name: "ix_vote_current_position_revision_id_vote_id",
                table: "vote_current",
                columns: new[] { "position_revision_id", "vote_id" });

            migrationBuilder.CreateIndex(
                name: "ix_vote_details_revision_origin_decision_id",
                table: "vote_details_revision",
                column: "origin_decision_id");

            migrationBuilder.CreateIndex(
                name: "ix_vote_details_revision_origin_import_row_id",
                table: "vote_details_revision",
                column: "origin_import_row_id");

            migrationBuilder.CreateIndex(
                name: "ix_vote_details_revision_vote_id_id",
                table: "vote_details_revision",
                columns: new[] { "vote_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_conflict_country_vote_revision_compared_country_vote_revisi",
                table: "conflict",
                columns: new[] { "compared_country_vote_revision_id", "vote_id", "state_code" },
                principalTable: "country_vote_revision",
                principalColumns: new[] { "id", "vote_id", "state_code" });

            migrationBuilder.AddForeignKey(
                name: "fk_conflict_vote_details_revision_compared_details_revision_id",
                table: "conflict",
                columns: new[] { "compared_details_revision_id", "vote_id" },
                principalTable: "vote_details_revision",
                principalColumns: new[] { "id", "vote_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_conflict_decision_country_vote_revision_replaced_country_vo",
                table: "conflict_decision",
                column: "replaced_country_vote_revision_id",
                principalTable: "country_vote_revision",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_conflict_decision_vote_details_revision_replaced_details_re",
                table: "conflict_decision",
                column: "replaced_details_revision_id",
                principalTable: "vote_details_revision",
                principalColumn: "id");

            migrationBuilder.Sql(InitialSchemaSql.DeferredRowKeys);
            migrationBuilder.Sql(InitialSchemaSql.Views);
            migrationBuilder.Sql(InitialSchemaSql.Users);
            migrationBuilder.Sql(InitialSchemaSql.Guard);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(InitialSchemaSql.Drop);
            migrationBuilder.Sql(InitialSchemaSql.DropGuardFunction);

            migrationBuilder.DropForeignKey(
                name: "fk_conflict_country_vote_revision_compared_country_vote_revisi",
                table: "conflict");

            migrationBuilder.DropForeignKey(
                name: "fk_conflict_decision_country_vote_revision_replaced_country_vo",
                table: "conflict_decision");

            migrationBuilder.DropForeignKey(
                name: "fk_conflict_import_opened_by_import_id",
                table: "conflict");

            migrationBuilder.DropForeignKey(
                name: "fk_import_row_import_import_id",
                table: "import_row");

            migrationBuilder.DropForeignKey(
                name: "fk_vote_import_first_seen_import_id",
                table: "vote");

            migrationBuilder.DropForeignKey(
                name: "fk_conflict_vote_details_revision_compared_details_revision_id",
                table: "conflict");

            migrationBuilder.DropForeignKey(
                name: "fk_conflict_decision_vote_details_revision_replaced_details_re",
                table: "conflict_decision");

            migrationBuilder.DropTable(
                name: "country_vote_current");

            migrationBuilder.DropTable(
                name: "decision_attempt_not_applied");

            migrationBuilder.DropTable(
                name: "import_parse");

            migrationBuilder.DropTable(
                name: "import_row_conflict");

            migrationBuilder.DropTable(
                name: "import_warning");

            migrationBuilder.DropTable(
                name: "job");

            migrationBuilder.DropTable(
                name: "membership_period");

            migrationBuilder.DropTable(
                name: "publication");

            migrationBuilder.DropTable(
                name: "snapshot_line");

            migrationBuilder.DropTable(
                name: "snapshot_result");

            migrationBuilder.DropTable(
                name: "snapshot_warning");

            migrationBuilder.DropTable(
                name: "vote_current");

            migrationBuilder.DropTable(
                name: "snapshot_vote");

            migrationBuilder.DropTable(
                name: "position_revision");

            migrationBuilder.DropTable(
                name: "snapshot");

            migrationBuilder.DropTable(
                name: "country_vote_revision");

            migrationBuilder.DropTable(
                name: "member_state");

            migrationBuilder.DropTable(
                name: "import");

            migrationBuilder.DropTable(
                name: "vote_details_revision");

            migrationBuilder.DropTable(
                name: "conflict_decision");

            migrationBuilder.DropTable(
                name: "import_row");

            migrationBuilder.DropTable(
                name: "app_user");

            migrationBuilder.DropTable(
                name: "conflict");

            migrationBuilder.DropTable(
                name: "vote");

            migrationBuilder.DropSequence(
                name: "history_seq");
        }
    }
}
