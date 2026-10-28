using Meridian.Integration.Tests.Infrastructure;
using Npgsql;

namespace Meridian.Integration.Tests.Database;

public sealed class JobTableTests(PostgresFixture postgres)
{
    [Fact]
    public async Task A_job_queued_in_plain_sql_starts_with_zero_counters()
    {
        await using var connection = new NpgsqlConnection(await postgres.CreateDatabaseAsync());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("""
            WITH i AS (
              INSERT INTO import (file_name, blob_name, sha256, size_bytes, uploaded_by)
              VALUES ('x.csv', 'imports/1/x.csv', repeat('0', 64), 1, 'dana') RETURNING id)
            INSERT INTO job (kind, import_id, state) SELECT 'import', id, 'queued' FROM i
            RETURNING rows_read, votes_done, attempts
            """, connection);

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal((0, 0, 0), (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2)));
    }
}
