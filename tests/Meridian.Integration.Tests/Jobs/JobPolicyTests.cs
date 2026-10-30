using Meridian.Domain;
using Meridian.Platform.Jobs;

namespace Meridian.Integration.Tests.Jobs;

public sealed class JobPolicyTests
{
    private static readonly JobOptions Options = new()
    {
        MaxConcurrentImports = 3,
        ImportTimeout = TimeSpan.FromMinutes(7),
        SnapshotTimeout = TimeSpan.FromMinutes(5),
    };

    [Fact]
    public void Every_job_kind_has_a_policy_so_a_new_kind_cannot_be_claimed_without_one()
    {
        foreach (var kind in Enum.GetValues<JobKind>())
            Assert.Equal(kind, JobPolicies.For(kind, Options).Kind);
    }

    [Fact]
    public void Imports_run_up_to_the_configured_limit_and_snapshots_one_at_a_time()
    {
        Assert.Equal(3, JobPolicies.For(JobKind.Import, Options).MaxRunning);
        Assert.Equal(1, JobPolicies.For(JobKind.Snapshot, Options).MaxRunning);
    }

    [Fact]
    public void Each_kind_has_its_own_time_limit_from_settings()
    {
        Assert.Equal(TimeSpan.FromMinutes(7), JobPolicies.For(JobKind.Import, Options).Timeout);
        Assert.Equal(TimeSpan.FromMinutes(5), JobPolicies.For(JobKind.Snapshot, Options).Timeout);
    }

    [Fact]
    public void Logs_carry_the_id_of_what_the_job_works_on_under_the_kind_s_own_name()
    {
        var import = new ClaimedJob(1, JobKind.Import, ImportId: 40, SnapshotId: null, Attempts: 1, TraceParent: null);
        var snapshot = new ClaimedJob(2, JobKind.Snapshot, ImportId: null, SnapshotId: 9, Attempts: 1, TraceParent: null);

        var importPolicy = JobPolicies.For(JobKind.Import, Options);
        var snapshotPolicy = JobPolicies.For(JobKind.Snapshot, Options);

        Assert.Equal(("ImportId", (long?)40), (importPolicy.LogScopeKey, importPolicy.LinkId(import)));
        Assert.Equal(("SnapshotId", (long?)9), (snapshotPolicy.LogScopeKey, snapshotPolicy.LinkId(snapshot)));
    }
}
