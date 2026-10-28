namespace Meridian.Data.Migrations;

/// <summary>The parts of migration 1 that EF can't model, as SQL.</summary>
internal static class InitialSchemaSql
{
    /// <summary>
    /// A row record points at the revision it created, whose origin is that same row, so
    /// these two keys are checked at commit (ids are reserved from history_seq first).
    /// </summary>
    public const string DeferredRowKeys = """
        ALTER TABLE import_row
          ADD CONSTRAINT fk_import_row_country_vote_revision FOREIGN KEY (country_vote_revision_id)
            REFERENCES country_vote_revision (id) DEFERRABLE INITIALLY DEFERRED,
          ADD CONSTRAINT fk_import_row_details_revision FOREIGN KEY (details_revision_id)
            REFERENCES vote_details_revision (id) DEFERRABLE INITIALLY DEFERRED;
        """;

    public const string Users = """
        INSERT INTO app_user (id, display_name, role) VALUES
          ('dana', 'Dana', 'analyst'),
          ('omar', 'Omar', 'analyst'),
          ('lena', 'Lena', 'admin');
        """;

    /// <summary>
    /// History is append-only: every table except the queue, the two pointer tables and
    /// EF's own refuses UPDATE, DELETE and TRUNCATE. The trigger is per statement, so it
    /// refuses even a statement that matches no row. Row locks (SELECT … FOR UPDATE)
    /// don't fire it. A table added later needs its own trigger; the guard test fails
    /// until it has one.
    /// </summary>
    public const string Guard = """
        CREATE FUNCTION refuse_history_change() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          RAISE EXCEPTION 'history is append-only: % on % is refused', TG_OP, TG_TABLE_NAME
            USING HINT = 'Insert a new revision and move its pointer instead.';
        END $$;

        DO $$
        DECLARE t text;
        BEGIN
          FOR t IN
            SELECT tablename FROM pg_tables
            WHERE schemaname = 'public'
              AND tablename NOT IN ('job', 'vote_current', 'country_vote_current')
              AND tablename NOT LIKE '\_\_EF%'
          LOOP
            EXECUTE format(
              'CREATE TRIGGER guard_history BEFORE UPDATE OR DELETE OR TRUNCATE ON %I '
              'FOR EACH STATEMENT EXECUTE FUNCTION refuse_history_change()', t);
          END LOOP;
        END $$;
        """;

    public const string Views = """
        CREATE VIEW current_country_vote AS
        SELECT p.vote_id, p.state_code, p.revision_id, r.ballot,
               r.origin_import_row_id, r.origin_decision_id, r.recorded_at
        FROM country_vote_current p
        JOIN country_vote_revision r ON r.id = p.revision_id;

        CREATE VIEW current_vote AS
        SELECT v.id AS vote_id, v.assembly_vote_id,
               d.id AS details_revision_id, d.session, d.vote_date, d.title,
               p.id AS position_revision_id, p.stance, p.weight
        FROM vote v
        JOIN vote_current c ON c.vote_id = v.id
        JOIN vote_details_revision d ON d.id = c.details_revision_id
        LEFT JOIN position_revision p ON p.id = c.position_revision_id;

        CREATE VIEW pending_conflict AS
        SELECT c.id AS conflict_id, c.vote_id, c.subject, c.opened_by_import_id,
               c.state_code, c.incoming_ballot, c.compared_country_vote_revision_id,
               c.incoming_session, c.incoming_vote_date, c.incoming_title, c.compared_details_revision_id,
               c.recorded_at,
               cv.revision_id AS current_country_vote_revision_id, cvr.ballot AS current_ballot,
               vc.details_revision_id AS current_details_revision_id,
               dr.session AS current_session, dr.vote_date AS current_vote_date, dr.title AS current_title
        FROM conflict c
        LEFT JOIN country_vote_current cv
          ON c.subject = 'ballot' AND cv.vote_id = c.vote_id AND cv.state_code = c.state_code
        LEFT JOIN country_vote_revision cvr ON cvr.id = cv.revision_id
        LEFT JOIN vote_current vc ON c.subject = 'details' AND vc.vote_id = c.vote_id
        LEFT JOIN vote_details_revision dr ON dr.id = vc.details_revision_id
        WHERE NOT EXISTS (SELECT 1 FROM conflict_decision d WHERE d.conflict_id = c.id);

        -- Every event on a vote's story in common columns, ordered by history_id (one
        -- shared sequence, exact per vote because of the per-vote lock). From → to
        -- pairs are built by the API from consecutive revisions.
        CREATE VIEW vote_timeline AS
        SELECT c.vote_id, c.history_id, 'import'::text AS kind, i.uploaded_by AS actor, i.recorded_at AS at,
               jsonb_build_object('importId', i.id, 'fileName', i.file_name, 'counts', c.counts) AS detail
        FROM (
          SELECT import_id, vote_id, min(first_id) AS history_id, jsonb_object_agg(outcome, n) AS counts
          FROM (
            SELECT import_id, vote_id, outcome, count(*) AS n, min(id) AS first_id
            FROM import_row WHERE vote_id IS NOT NULL
            GROUP BY import_id, vote_id, outcome
          ) per_outcome
          GROUP BY import_id, vote_id
        ) c
        JOIN import i ON i.id = c.import_id

        UNION ALL
        SELECT d.vote_id, d.id, 'details_revision', coalesce(oi.uploaded_by, od.decided_by), d.recorded_at,
               jsonb_build_object('revisionId', d.id, 'session', d.session, 'voteDate', d.vote_date, 'title', d.title,
                 'originImportRowId', d.origin_import_row_id, 'originImportId', orow.import_id,
                 'originLineNo', orow.line_no, 'originDecisionId', d.origin_decision_id)
        FROM vote_details_revision d
        LEFT JOIN import_row orow ON orow.id = d.origin_import_row_id
        LEFT JOIN import oi ON oi.id = orow.import_id
        LEFT JOIN conflict_decision od ON od.id = d.origin_decision_id

        UNION ALL
        SELECT r.vote_id, r.id, 'country_vote_revision', coalesce(oi.uploaded_by, od.decided_by), r.recorded_at,
               jsonb_build_object('revisionId', r.id, 'stateCode', r.state_code, 'ballot', r.ballot,
                 'originImportRowId', r.origin_import_row_id, 'originImportId', orow.import_id,
                 'originLineNo', orow.line_no, 'originDecisionId', r.origin_decision_id)
        FROM country_vote_revision r
        LEFT JOIN import_row orow ON orow.id = r.origin_import_row_id
        LEFT JOIN import oi ON oi.id = orow.import_id
        LEFT JOIN conflict_decision od ON od.id = r.origin_decision_id

        UNION ALL
        SELECT p.vote_id, p.id, 'position_revision', p.set_by, p.recorded_at,
               jsonb_build_object('revisionId', p.id, 'stance', p.stance, 'weight', p.weight)
        FROM position_revision p

        UNION ALL
        SELECT c.vote_id, c.id, 'conflict_opened', i.uploaded_by, c.recorded_at,
               jsonb_build_object('conflictId', c.id, 'subject', c.subject, 'importId', c.opened_by_import_id,
                 'stateCode', c.state_code, 'incomingBallot', c.incoming_ballot,
                 'comparedCountryVoteRevisionId', c.compared_country_vote_revision_id,
                 'incomingSession', c.incoming_session, 'incomingVoteDate', c.incoming_vote_date,
                 'incomingTitle', c.incoming_title, 'comparedDetailsRevisionId', c.compared_details_revision_id)
        FROM conflict c
        JOIN import i ON i.id = c.opened_by_import_id

        UNION ALL
        SELECT c.vote_id, d.id, 'conflict_decided', d.decided_by, d.recorded_at,
               jsonb_build_object('decisionId', d.id, 'conflictId', c.id, 'subject', c.subject,
                 'decision', d.decision, 'comment', d.comment,
                 'replacedCountryVoteRevisionId', d.replaced_country_vote_revision_id,
                 'replacedDetailsRevisionId', d.replaced_details_revision_id)
        FROM conflict_decision d
        JOIN conflict c ON c.id = d.conflict_id

        UNION ALL
        SELECT c.vote_id, a.id, 'decision_not_applied', a.attempted_by, a.recorded_at,
               jsonb_build_object('attemptId', a.id, 'conflictId', c.id, 'subject', c.subject, 'attempted', a.attempted)
        FROM decision_attempt_not_applied a
        JOIN conflict c ON c.id = a.conflict_id

        UNION ALL
        SELECT sv.vote_id, s.id, 'snapshot', s.created_by, s.recorded_at,
               jsonb_build_object('snapshotId', s.id, 'name', s.name,
                 'detailsRevisionId', sv.details_revision_id, 'positionRevisionId', sv.position_revision_id)
        FROM snapshot_vote sv
        JOIN snapshot s ON s.id = sv.snapshot_id

        UNION ALL
        SELECT sv.vote_id, p.id, 'publication', p.published_by, p.recorded_at,
               jsonb_build_object('publicationId', p.id, 'snapshotId', p.snapshot_id, 'quarter', p.quarter,
                 'reason', p.reason, 'supersedesPublicationId', p.supersedes_publication_id)
        FROM publication p
        JOIN snapshot_vote sv ON sv.snapshot_id = p.snapshot_id;
        """;

    public const string Drop = """
        DROP VIEW IF EXISTS vote_timeline, pending_conflict, current_vote, current_country_vote;
        """;

    public const string DropGuardFunction = "DROP FUNCTION IF EXISTS refuse_history_change() CASCADE;";
}
