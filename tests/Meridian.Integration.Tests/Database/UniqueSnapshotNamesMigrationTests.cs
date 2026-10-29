using Meridian.Data;
using Meridian.Data.Migrations;
using Meridian.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Meridian.Integration.Tests.Database;

/// <summary>
/// Snapshots are insert-only, so names shared before the unique index existed cannot be
/// renamed: the migration leaves them be and holds every other name.
/// </summary>
public sealed class UniqueSnapshotNamesMigrationTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Names_already_shared_survive_the_migration_and_every_other_name_is_held()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        await using var db = new MeridianDbContext(MeridianDb.Options(connectionString, PostgresFixture.MemberStatesPath));
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261028201624_InitialSchema", cancellationToken: Ct);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await InsertAsync(connection, "June 2020");
        await InsertAsync(connection, " june 2020 ");
        await InsertAsync(connection, "July 2020");

        await migrator.MigrateAsync(cancellationToken: Ct);

        await InsertAsync(connection, "August 2020");
        foreach (var taken in new[] { "AUGUST 2020", "July 2020" })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(connection, taken));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
            Assert.Equal(UniqueSnapshotNames.IndexName, error.ConstraintName);
        }
        Assert.Equal(4L, await ScalarAsync(connection, "SELECT count(*) FROM snapshot"));
    }

    [Fact]
    public async Task Down_drops_the_index()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        await using var db = new MeridianDbContext(MeridianDb.Options(connectionString, PostgresFixture.MemberStatesPath));
        await db.Database.MigrateAsync(Ct);

        await db.GetService<IMigrator>().MigrateAsync("20261028201624_InitialSchema", cancellationToken: Ct);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        Assert.Equal(0L, await ScalarAsync(connection, $"SELECT count(*) FROM pg_indexes WHERE indexname = '{UniqueSnapshotNames.IndexName}'"));
    }

    private static async Task InsertAsync(NpgsqlConnection connection, string name)
    {
        await using var command = new NpgsqlCommand(
            "INSERT INTO snapshot (name, selection, scoring_rules_version, created_by) VALUES (@name, '{}', 'v1', 'dana')", connection);
        command.Parameters.AddWithValue("name", name);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<long> ScalarAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }
}
