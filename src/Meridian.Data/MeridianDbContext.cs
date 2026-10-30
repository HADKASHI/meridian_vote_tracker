using Meridian.Data.Entities;
using Meridian.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meridian.Data;

/// <summary>
/// The whole locked schema (docs/schema.md). What EF can't express (the guard trigger,
/// the views, the shared sequence default and the deferred row-to-revision keys) is SQL
/// inside the migrations.
/// </summary>
public sealed class MeridianDbContext(DbContextOptions<MeridianDbContext> options) : DbContext(options)
{
    public const string HistorySequence = "history_seq";

    public DbSet<MemberState> MemberStates => Set<MemberState>();
    public DbSet<MembershipPeriod> MembershipPeriods => Set<MembershipPeriod>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Import> Imports => Set<Import>();
    public DbSet<ImportParse> ImportParses => Set<ImportParse>();
    public DbSet<ImportRow> ImportRows => Set<ImportRow>();
    public DbSet<ImportWarning> ImportWarnings => Set<ImportWarning>();
    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<VoteDetailsRevision> VoteDetailsRevisions => Set<VoteDetailsRevision>();
    public DbSet<PositionRevision> PositionRevisions => Set<PositionRevision>();
    public DbSet<CountryVoteRevision> CountryVoteRevisions => Set<CountryVoteRevision>();
    public DbSet<VoteCurrent> VoteCurrents => Set<VoteCurrent>();
    public DbSet<CountryVoteCurrent> CountryVoteCurrents => Set<CountryVoteCurrent>();
    public DbSet<Conflict> Conflicts => Set<Conflict>();
    public DbSet<ImportRowConflict> ImportRowConflicts => Set<ImportRowConflict>();
    public DbSet<ConflictDecision> ConflictDecisions => Set<ConflictDecision>();
    public DbSet<DecisionAttemptNotApplied> DecisionAttemptsNotApplied => Set<DecisionAttemptNotApplied>();
    public DbSet<Snapshot> Snapshots => Set<Snapshot>();
    public DbSet<SnapshotVote> SnapshotVotes => Set<SnapshotVote>();
    public DbSet<SnapshotLine> SnapshotLines => Set<SnapshotLine>();
    public DbSet<SnapshotResult> SnapshotResults => Set<SnapshotResult>();
    public DbSet<SnapshotWarning> SnapshotWarnings => Set<SnapshotWarning>();
    public DbSet<Publication> Publications => Set<Publication>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Extraction> Extractions => Set<Extraction>();

    public DbSet<CurrentCountryVote> CurrentCountryVotes => Set<CurrentCountryVote>();
    public DbSet<CurrentVote> CurrentVotes => Set<CurrentVote>();
    public DbSet<PendingConflict> PendingConflicts => Set<PendingConflict>();
    public DbSet<VoteTimelineEvent> VoteTimeline => Set<VoteTimelineEvent>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configuration)
    {
        configuration.Properties<Outcome>().HaveConversion<SnakeCaseEnumConverter<Outcome>>();
        configuration.Properties<Stance>().HaveConversion<SnakeCaseEnumConverter<Stance>>();
        configuration.Properties<ConflictSubject>().HaveConversion<SnakeCaseEnumConverter<ConflictSubject>>();
        configuration.Properties<Decision>().HaveConversion<SnakeCaseEnumConverter<Decision>>();
        configuration.Properties<LineCategory>().HaveConversion<SnakeCaseEnumConverter<LineCategory>>();
        configuration.Properties<Role>().HaveConversion<SnakeCaseEnumConverter<Role>>();
        configuration.Properties<JobKind>().HaveConversion<SnakeCaseEnumConverter<JobKind>>();
        configuration.Properties<JobState>().HaveConversion<SnakeCaseEnumConverter<JobState>>();
        configuration.Properties<ExtractionStatus>().HaveConversion<SnakeCaseEnumConverter<ExtractionStatus>>();
        configuration.Properties<Ballot>().HaveConversion<string>().HaveColumnType("char(1)");
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasSequence<long>(HistorySequence);

        ConfigureReference(model);
        ConfigureIngestion(model);
        ConfigureApprovedData(model);
        ConfigureConflicts(model);
        ConfigureSnapshots(model);
        ConfigureJob(model);
        ConfigureExtraction(model);
        ConfigureViews(model);
    }

    private static void ConfigureReference(ModelBuilder model)
    {
        model.Entity<MemberState>(e =>
        {
            e.ToTable("member_state", t => t.HasCheckConstraint("ck_member_state_code", "code ~ '^[A-Z]{3}$'"));
            e.HasKey(x => x.Code);
            e.Property(x => x.Code).HasColumnType("char(3)");
        });

        model.Entity<MembershipPeriod>(e =>
        {
            e.ToTable("membership_period", t => t.HasCheckConstraint("ck_membership_period_range", "member_to >= member_from"));
            HistoryId(e.Property(x => x.Id));
            e.HasOne<MemberState>().WithMany().HasForeignKey(x => x.StateCode);
            e.Property(x => x.StateCode).HasColumnType("char(3)");
            e.HasIndex(x => x.StateCode);
        });

        model.Entity<AppUser>(e =>
        {
            e.ToTable("app_user", t => t.HasCheckConstraint("ck_app_user_role", $"role IN ({SnakeCaseEnumConverter<Role>.SqlList()})"));
        });
    }

    private static void ConfigureIngestion(ModelBuilder model)
    {
        model.Entity<Import>(e =>
        {
            e.ToTable("import");
            HistoryId(e.Property(x => x.Id));
            e.Property(x => x.Sha256).HasColumnType("char(64)");
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UploadedBy);
            e.HasIndex(x => x.Sha256);
            RecordedAt(e.Property(x => x.RecordedAt));
        });

        model.Entity<ImportParse>(e =>
        {
            e.ToTable("import_parse");
            e.HasKey(x => x.ImportId);
            e.HasOne<Import>().WithOne().HasForeignKey<ImportParse>(x => x.ImportId);
            e.Property(x => x.Header).HasColumnType("jsonb");
            RecordedAt(e.Property(x => x.RecordedAt));
        });

        model.Entity<ImportRow>(e =>
        {
            e.ToTable("import_row", t => t.HasCheckConstraint("ck_import_row_outcome", $"outcome IN ({SnakeCaseEnumConverter<Outcome>.SqlList()})"));
            HistoryId(e.Property(x => x.Id));
            e.HasOne<Import>().WithMany().HasForeignKey(x => x.ImportId);
            e.HasOne<Vote>().WithMany().HasForeignKey(x => x.VoteId);
            e.HasAlternateKey(x => new { x.ImportId, x.LineNo });
            e.HasIndex(x => new { x.ImportId, x.Outcome });
            e.HasIndex(x => new { x.ImportId, x.VoteId });
            e.HasIndex(x => x.VoteId);
            e.Property(x => x.Normalisations).HasColumnType("jsonb");
            // country_vote_revision_id and details_revision_id reference revisions whose
            // origin is this row, so their foreign keys are deferred (SQL in the migration).
            RecordedAt(e.Property(x => x.RecordedAt));
        });

        model.Entity<ImportWarning>(e =>
        {
            e.ToTable("import_warning");
            HistoryId(e.Property(x => x.Id));
            e.HasOne<Import>().WithMany().HasForeignKey(x => x.ImportId);
            e.HasOne<Vote>().WithMany().HasForeignKey(x => x.VoteId);
            e.HasIndex(x => x.ImportId);
            e.Property(x => x.Detail).HasColumnType("jsonb");
            RecordedAt(e.Property(x => x.RecordedAt));
        });
    }

    private static void ConfigureApprovedData(ModelBuilder model)
    {
        model.Entity<Vote>(e =>
        {
            e.ToTable("vote");
            HistoryId(e.Property(x => x.Id));
            e.HasIndex(x => x.AssemblyVoteId).IsUnique();
            e.HasOne<Import>().WithMany().HasForeignKey(x => x.FirstSeenImportId);
            RecordedAt(e.Property(x => x.RecordedAt));
        });

        model.Entity<VoteDetailsRevision>(e =>
        {
            e.ToTable("vote_details_revision", t =>
            {
                t.HasCheckConstraint("ck_vote_details_revision_session", "session > 0");
                t.HasCheckConstraint("ck_vote_details_revision_one_origin", "num_nonnulls(origin_import_row_id, origin_decision_id) = 1");
            });
            HistoryId(e.Property(x => x.Id));
            e.HasOne<Vote>().WithMany().HasForeignKey(x => x.VoteId);
            e.HasAlternateKey(x => new { x.Id, x.VoteId });
            e.HasIndex(x => new { x.VoteId, x.Id });
            e.HasOne<ImportRow>().WithMany().HasForeignKey(x => x.OriginImportRowId);
            e.HasOne<ConflictDecision>().WithMany().HasForeignKey(x => x.OriginDecisionId);
            RecordedAt(e.Property(x => x.RecordedAt));
        });

        model.Entity<PositionRevision>(e =>
        {
            e.ToTable("position_revision", t =>
            {
                t.HasCheckConstraint("ck_position_revision_stance", $"stance IN ({SnakeCaseEnumConverter<Stance>.SqlList()})");
                t.HasCheckConstraint("ck_position_revision_weight", "weight BETWEEN 1 AND 5");
            });
            HistoryId(e.Property(x => x.Id));
            e.HasOne<Vote>().WithMany().HasForeignKey(x => x.VoteId);
            e.HasAlternateKey(x => new { x.Id, x.VoteId });
            e.HasIndex(x => new { x.VoteId, x.Id });
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.SetBy);
            RecordedAt(e.Property(x => x.RecordedAt));
        });

        model.Entity<CountryVoteRevision>(e =>
        {
            e.ToTable("country_vote_revision", t =>
            {
                t.HasCheckConstraint("ck_country_vote_revision_ballot", "ballot IN ('Y', 'N', 'A', 'X')");
                t.HasCheckConstraint("ck_country_vote_revision_one_origin", "num_nonnulls(origin_import_row_id, origin_decision_id) = 1");
            });
            HistoryId(e.Property(x => x.Id));
            e.HasOne<Vote>().WithMany().HasForeignKey(x => x.VoteId);
            e.HasOne<MemberState>().WithMany().HasForeignKey(x => x.StateCode);
            e.Property(x => x.StateCode).HasColumnType("char(3)");
            e.HasAlternateKey(x => new { x.Id, x.VoteId, x.StateCode });
            e.HasIndex(x => new { x.VoteId, x.StateCode, x.Id });
            e.HasOne<ImportRow>().WithMany().HasForeignKey(x => x.OriginImportRowId);
            e.HasOne<ConflictDecision>().WithMany().HasForeignKey(x => x.OriginDecisionId);
            RecordedAt(e.Property(x => x.RecordedAt));
        });

        model.Entity<VoteCurrent>(e =>
        {
            e.ToTable("vote_current");
            e.HasKey(x => x.VoteId);
            e.Property(x => x.VoteId).ValueGeneratedNever();
            e.HasOne<Vote>().WithOne().HasForeignKey<VoteCurrent>(x => x.VoteId);
            // Composite keys: a pointer can only reach its own vote's revisions.
            e.HasOne<VoteDetailsRevision>().WithMany()
                .HasForeignKey(x => new { x.DetailsRevisionId, x.VoteId })
                .HasPrincipalKey(r => new { r.Id, r.VoteId });
            e.HasOne<PositionRevision>().WithMany()
                .HasForeignKey(x => new { x.PositionRevisionId, x.VoteId })
                .HasPrincipalKey(r => new { r.Id, r.VoteId });
        });

        model.Entity<CountryVoteCurrent>(e =>
        {
            e.ToTable("country_vote_current");
            e.HasKey(x => new { x.VoteId, x.StateCode });
            e.Property(x => x.StateCode).HasColumnType("char(3)");
            e.HasOne<CountryVoteRevision>().WithMany()
                .HasForeignKey(x => new { x.RevisionId, x.VoteId, x.StateCode })
                .HasPrincipalKey(r => new { r.Id, r.VoteId, r.StateCode });
            e.HasIndex(x => x.StateCode);
        });
    }

    private static void ConfigureConflicts(ModelBuilder model)
    {
        model.Entity<Conflict>(e =>
        {
            e.ToTable("conflict", t =>
            {
                t.HasCheckConstraint("ck_conflict_subject", $"subject IN ({SnakeCaseEnumConverter<ConflictSubject>.SqlList()})");
                t.HasCheckConstraint("ck_conflict_subject_columns", """
                    (subject = 'ballot'
                      AND num_nonnulls(state_code, incoming_ballot, compared_country_vote_revision_id) = 3
                      AND num_nonnulls(incoming_session, incoming_vote_date, incoming_title, incoming_title_key, compared_details_revision_id) = 0)
                    OR (subject = 'details'
                      AND num_nonnulls(state_code, incoming_ballot, compared_country_vote_revision_id) = 0
                      AND num_nonnulls(incoming_session, incoming_vote_date, incoming_title, incoming_title_key, compared_details_revision_id) = 5)
                    """);
                t.HasCheckConstraint("ck_conflict_incoming_ballot", "incoming_ballot IN ('Y', 'N', 'A', 'X')");
            });
            HistoryId(e.Property(x => x.Id));
            e.HasOne<Vote>().WithMany().HasForeignKey(x => x.VoteId);
            e.HasOne<Import>().WithMany().HasForeignKey(x => x.OpenedByImportId);
            e.Property(x => x.StateCode).HasColumnType("char(3)");
            e.HasOne<CountryVoteRevision>().WithMany()
                .HasForeignKey(x => new { x.ComparedCountryVoteRevisionId, x.VoteId, x.StateCode })
                .HasPrincipalKey(r => new { r.Id, r.VoteId, r.StateCode });
            e.HasOne<VoteDetailsRevision>().WithMany()
                .HasForeignKey(x => new { x.ComparedDetailsRevisionId, x.VoteId })
                .HasPrincipalKey(r => new { r.Id, r.VoteId });
            // ING-12: an incoming value matching a conflict against the same approved
            // revision links to it, pending or decided.
            e.HasIndex(x => new { x.ComparedCountryVoteRevisionId, x.IncomingBallot })
                .IsUnique().HasFilter("subject = 'ballot'").HasDatabaseName("ux_conflict_ballot_incoming");
            e.HasIndex(x => new { x.ComparedDetailsRevisionId, x.IncomingSession, x.IncomingVoteDate, x.IncomingTitleKey })
                .IsUnique().HasFilter("subject = 'details'").HasDatabaseName("ux_conflict_details_incoming");
            e.HasIndex(x => x.VoteId);
            RecordedAt(e.Property(x => x.RecordedAt));
        });

        model.Entity<ImportRowConflict>(e =>
        {
            e.ToTable("import_row_conflict");
            e.HasKey(x => new { x.ImportRowId, x.ConflictId });
            e.HasOne<ImportRow>().WithMany().HasForeignKey(x => x.ImportRowId);
            e.HasOne<Conflict>().WithMany().HasForeignKey(x => x.ConflictId);
            e.HasIndex(x => x.ConflictId);
        });

        model.Entity<ConflictDecision>(e =>
        {
            e.ToTable("conflict_decision", t =>
                t.HasCheckConstraint("ck_conflict_decision_decision", $"decision IN ({SnakeCaseEnumConverter<Decision>.SqlList()})"));
            HistoryId(e.Property(x => x.Id));
            // CON-05: one decision per conflict; the loser of a race hits this index.
            e.HasOne<Conflict>().WithOne().HasForeignKey<ConflictDecision>(x => x.ConflictId);
            e.HasIndex(x => x.ConflictId).IsUnique();
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.DecidedBy);
            e.HasOne<CountryVoteRevision>().WithMany().HasForeignKey(x => x.ReplacedCountryVoteRevisionId);
            e.HasOne<VoteDetailsRevision>().WithMany().HasForeignKey(x => x.ReplacedDetailsRevisionId);
            RecordedAt(e.Property(x => x.RecordedAt));
        });

        model.Entity<DecisionAttemptNotApplied>(e =>
        {
            e.ToTable("decision_attempt_not_applied", t =>
                t.HasCheckConstraint("ck_decision_attempt_not_applied_attempted", $"attempted IN ({SnakeCaseEnumConverter<Decision>.SqlList()})"));
            HistoryId(e.Property(x => x.Id));
            e.HasOne<Conflict>().WithMany().HasForeignKey(x => x.ConflictId);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.AttemptedBy);
            e.HasIndex(x => x.ConflictId);
            RecordedAt(e.Property(x => x.RecordedAt));
        });
    }

    private static void ConfigureSnapshots(ModelBuilder model)
    {
        model.Entity<Snapshot>(e =>
        {
            e.ToTable("snapshot");
            HistoryId(e.Property(x => x.Id));
            e.Property(x => x.Selection).HasColumnType("jsonb");
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedBy);
            RecordedAt(e.Property(x => x.RecordedAt));
        });

        model.Entity<SnapshotVote>(e =>
        {
            e.ToTable("snapshot_vote");
            e.HasKey(x => new { x.SnapshotId, x.VoteId });
            e.HasOne<Snapshot>().WithMany().HasForeignKey(x => x.SnapshotId);
            e.HasOne<Vote>().WithMany().HasForeignKey(x => x.VoteId);
            e.HasOne<VoteDetailsRevision>().WithMany()
                .HasForeignKey(x => new { x.DetailsRevisionId, x.VoteId })
                .HasPrincipalKey(r => new { r.Id, r.VoteId });
            e.HasOne<PositionRevision>().WithMany()
                .HasForeignKey(x => new { x.PositionRevisionId, x.VoteId })
                .HasPrincipalKey(r => new { r.Id, r.VoteId });
        });

        model.Entity<SnapshotLine>(e =>
        {
            e.ToTable("snapshot_line", t =>
                t.HasCheckConstraint("ck_snapshot_line_category", $"category IN ({SnakeCaseEnumConverter<LineCategory>.SqlList()})"));
            e.HasKey(x => new { x.SnapshotId, x.VoteId, x.StateCode });
            e.Property(x => x.StateCode).HasColumnType("char(3)");
            e.HasOne<SnapshotVote>().WithMany().HasForeignKey(x => new { x.SnapshotId, x.VoteId });
            e.HasOne<MemberState>().WithMany().HasForeignKey(x => x.StateCode);
            e.HasOne<CountryVoteRevision>().WithMany()
                .HasForeignKey(x => new { x.CountryVoteRevisionId, x.VoteId, x.StateCode })
                .HasPrincipalKey(r => new { r.Id, r.VoteId, r.StateCode });
            e.HasIndex(x => new { x.SnapshotId, x.StateCode });
        });

        model.Entity<SnapshotResult>(e =>
        {
            e.ToTable("snapshot_result", t =>
            {
                t.HasCheckConstraint("ck_snapshot_result_weights", "0 <= matched_weight AND matched_weight <= counted_weight");
                t.HasCheckConstraint("ck_snapshot_result_rank", "(rank IS NULL) = (counted_weight = 0)");
            });
            e.HasKey(x => new { x.SnapshotId, x.StateCode });
            e.Property(x => x.StateCode).HasColumnType("char(3)");
            e.HasOne<Snapshot>().WithMany().HasForeignKey(x => x.SnapshotId);
            e.HasOne<MemberState>().WithMany().HasForeignKey(x => x.StateCode);
        });

        model.Entity<SnapshotWarning>(e =>
        {
            e.ToTable("snapshot_warning");
            HistoryId(e.Property(x => x.Id));
            e.HasOne<Snapshot>().WithMany().HasForeignKey(x => x.SnapshotId);
            e.HasOne<Vote>().WithMany().HasForeignKey(x => x.VoteId);
            e.HasIndex(x => x.SnapshotId);
            e.Property(x => x.Detail).HasColumnType("jsonb");
            RecordedAt(e.Property(x => x.RecordedAt));
        });

        model.Entity<Publication>(e =>
        {
            e.ToTable("publication", t =>
            {
                t.HasCheckConstraint("ck_publication_quarter", "quarter ~ '^[0-9]{4}-Q[1-4]$'");
                t.HasCheckConstraint("ck_publication_reason", "(supersedes_publication_id IS NULL) = (reason IS NULL)");
            });
            HistoryId(e.Property(x => x.Id));
            e.HasOne<Snapshot>().WithMany().HasForeignKey(x => x.SnapshotId);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.PublishedBy);
            // A replacement stays in its quarter.
            e.HasOne<Publication>().WithMany()
                .HasForeignKey(x => new { x.SupersedesPublicationId, x.Quarter })
                .HasPrincipalKey(p => new { p.Id, p.Quarter });
            e.HasIndex(x => x.Quarter).IsUnique().HasFilter("supersedes_publication_id IS NULL")
                .HasDatabaseName("ux_publication_first_per_quarter");
            e.HasIndex(x => x.SupersedesPublicationId).IsUnique().HasFilter("supersedes_publication_id IS NOT NULL")
                .HasDatabaseName("ux_publication_one_successor");
            RecordedAt(e.Property(x => x.RecordedAt));
        });
    }

    private static void ConfigureJob(ModelBuilder model)
    {
        model.Entity<Job>(e =>
        {
            e.ToTable("job", t =>
            {
                t.HasCheckConstraint("ck_job_kind", $"kind IN ({SnakeCaseEnumConverter<JobKind>.SqlList()})");
                t.HasCheckConstraint("ck_job_state", $"state IN ({SnakeCaseEnumConverter<JobState>.SqlList()})");
                t.HasCheckConstraint("ck_job_link",
                    "(kind = 'import') = (import_id IS NOT NULL) AND (kind = 'snapshot') = (snapshot_id IS NOT NULL)"
                    + " AND (kind = 'pdf_extraction') = (extraction_id IS NOT NULL)");
            });
            e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.HasOne<Import>().WithOne().HasForeignKey<Job>(x => x.ImportId);
            e.HasOne<Snapshot>().WithOne().HasForeignKey<Job>(x => x.SnapshotId);
            e.HasOne<Extraction>().WithOne().HasForeignKey<Job>(x => x.ExtractionId);
            e.HasIndex(x => x.ImportId).IsUnique();
            e.HasIndex(x => x.SnapshotId).IsUnique();
            e.HasIndex(x => x.ExtractionId).IsUnique();
            e.HasIndex(x => new { x.Kind, x.Id }).HasFilter("state = 'queued'").HasDatabaseName("ix_job_queued");
            e.HasIndex(x => x.HeartbeatAt).HasFilter("state = 'running'").HasDatabaseName("ix_job_running_heartbeat");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            // Defaults for inserts written in SQL; EF always sends its own values.
            foreach (var counter in new[] { nameof(Job.RowsRead), nameof(Job.VotesDone), nameof(Job.Attempts) })
                e.Property<int>(counter).HasDefaultValue(0).ValueGeneratedNever();
        });
    }

    /// <summary>A mutable workflow table like <c>job</c>, so outside the history guard (ADR 0008).</summary>
    private static void ConfigureExtraction(ModelBuilder model)
    {
        model.Entity<Extraction>(e =>
        {
            e.ToTable("extraction", t =>
            {
                t.HasCheckConstraint("ck_extraction_status", $"status IN ({SnakeCaseEnumConverter<ExtractionStatus>.SqlList()})");
                t.HasCheckConstraint("ck_extraction_decision", "(status IN ('approved', 'rejected')) = (decided_by IS NOT NULL)");
                t.HasCheckConstraint("ck_extraction_import", "(status = 'approved') = (import_id IS NOT NULL)");
            });
            e.Property(x => x.Id).HasDefaultValueSql($"nextval('{HistorySequence}')").ValueGeneratedOnAdd();
            e.Property(x => x.Extracted).HasColumnType("jsonb");
            e.Property(x => x.Sheet).HasColumnType("jsonb");
            e.Property(x => x.CheckResult).HasColumnType("jsonb");
            e.Property(x => x.Edits).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb");
            e.HasOne<Import>().WithOne().HasForeignKey<Extraction>(x => x.ImportId);
            e.HasIndex(x => x.Sha256).IsUnique().HasDatabaseName("ux_extraction_sha256");
            e.HasIndex(x => x.ImportId).IsUnique().HasFilter("import_id IS NOT NULL").HasDatabaseName("ux_extraction_import");
            e.HasIndex(x => x.Status).HasDatabaseName("ix_extraction_status");
            RecordedAt(e.Property(x => x.RecordedAt));
        });
    }

    private static void ConfigureViews(ModelBuilder model)
    {
        model.Entity<CurrentCountryVote>(e => { e.HasNoKey(); e.ToView("current_country_vote"); });
        model.Entity<CurrentVote>(e => { e.HasNoKey(); e.ToView("current_vote"); });
        model.Entity<PendingConflict>(e => { e.HasNoKey(); e.ToView("pending_conflict"); });
        model.Entity<VoteTimelineEvent>(e => { e.HasNoKey(); e.ToView("vote_timeline"); });
    }

    private static void HistoryId(PropertyBuilder<long> id) =>
        id.HasDefaultValueSql($"nextval('{HistorySequence}')").ValueGeneratedOnAdd();

    private static void RecordedAt(PropertyBuilder<DateTimeOffset> recordedAt) =>
        recordedAt.HasDefaultValueSql("now()").ValueGeneratedOnAdd();
}
