using Meridian.Integration.Tests.Infrastructure;
using Npgsql;

namespace Meridian.Integration.Tests.Database;

public sealed class GuardTests(PostgresFixture postgres)
{
    private static readonly string[] Allowlist =
        ["job", "vote_current", "country_vote_current", "__EFMigrationsHistory"];

    [Fact]
    public async Task Every_table_outside_the_allowlist_has_the_guard()
    {
        await using var connection = await OpenAsync();
        var unguarded = await QueryNamesAsync(connection, """
            SELECT c.relname FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public' AND c.relkind IN ('r', 'p')
              AND NOT EXISTS (
                SELECT 1 FROM pg_trigger t
                JOIN pg_proc p ON p.oid = t.tgfoid
                WHERE t.tgrelid = c.oid AND p.proname = 'refuse_history_change'
                  AND t.tgenabled <> 'D'
                  -- BEFORE (2) UPDATE (16), DELETE (8) and TRUNCATE (32)
                  AND (t.tgtype & 2) = 2 AND (t.tgtype & 56) = 56)
            ORDER BY 1
            """);

        Assert.Equal(Allowlist.Order(StringComparer.Ordinal), unguarded.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Update_delete_and_truncate_are_refused_on_every_guarded_table()
    {
        await using var connection = await OpenAsync();
        var guarded = await GuardedTablesAsync(connection);
        Assert.Contains("country_vote_revision", guarded);
        Assert.Contains("member_state", guarded);

        foreach (var table in guarded)
        {
            var column = (await QueryNamesAsync(connection,
                $"SELECT attname FROM pg_attribute WHERE attrelid = 'public.{table}'::regclass AND attnum > 0 AND NOT attisdropped ORDER BY attnum LIMIT 1"))[0];

            await AssertRefusedAsync(connection, table, $"UPDATE {table} SET {column} = {column}");
            await AssertRefusedAsync(connection, table, $"DELETE FROM {table}");
            await AssertRefusedAsync(connection, table, $"TRUNCATE {table} CASCADE");
        }
    }

    [Fact]
    public async Task Seeded_reference_rows_cannot_be_changed()
    {
        await using var connection = await OpenAsync();

        await AssertRefusedAsync(connection, "app_user", "UPDATE app_user SET role = 'admin' WHERE id = 'dana'");
        await AssertRefusedAsync(connection, "membership_period", "DELETE FROM membership_period WHERE state_code = 'NZL'");
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(await postgres.CreateDatabaseAsync());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private static async Task<List<string>> GuardedTablesAsync(NpgsqlConnection connection) =>
        (await QueryNamesAsync(connection, """
            SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public' AND c.relkind IN ('r', 'p') ORDER BY 1
            """)).Except(Allowlist).ToList();

    private static async Task AssertRefusedAsync(NpgsqlConnection connection, string table, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.True(error.MessageText.Contains("append-only", StringComparison.Ordinal),
            $"{sql} on {table} failed for another reason: {error.MessageText}");
    }

    private static async Task<List<string>> QueryNamesAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var names = new List<string>();
        while (await reader.ReadAsync()) names.Add(reader.GetString(0));
        return names;
    }
}
