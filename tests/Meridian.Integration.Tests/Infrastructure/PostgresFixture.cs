using Meridian.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Meridian.Integration.Tests.Infrastructure.PostgresFixture))]

namespace Meridian.Integration.Tests.Infrastructure;

/// <summary>
/// One PostgreSQL container per test run. Migrations and seeding run once into a
/// template database; every test copies it with CREATE DATABASE … TEMPLATE, because
/// the history guard refuses TRUNCATE-based resets by design.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string TemplateName = "meridian_template";

    public static string MemberStatesPath { get; } =
        Path.Combine(AppContext.BaseDirectory, "data", "member_states.csv");

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Environment.GetEnvironmentVariable("POSTGRES_IMAGE") is { Length: > 0 } image ? image : "postgres:18")
        .WithDatabase("postgres")
        .WithUsername("meridian")
        .WithPassword("meridian_test")
        // Every test database keeps its own pool of idle connections, so the
        // default limit of 100 runs out once the suite passes about 100 tests.
        .WithCommand("-c", "max_connections=500")
        .Build();

    private readonly SemaphoreSlim _createLock = new(1, 1);
    private int _counter;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await ExecuteOnServerAsync($"CREATE DATABASE {TemplateName}");
        await using var db = new MeridianDbContext(MeridianDb.Options(ConnectionStringFor(TemplateName), MemberStatesPath));
        await db.Database.MigrateAsync();
        NpgsqlConnection.ClearAllPools();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    public string ConnectionStringFor(string database) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = database }.ConnectionString;

    /// <summary>A fresh database copied from the migrated, seeded template.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = $"meridian_test_{Interlocked.Increment(ref _counter)}";
        // CREATE DATABASE … TEMPLATE fails while another session uses the template,
        // so copies are made one at a time.
        await _createLock.WaitAsync();
        try { await ExecuteOnServerAsync($"CREATE DATABASE {name} TEMPLATE {TemplateName}"); }
        finally { _createLock.Release(); }
        return ConnectionStringFor(name);
    }

    /// <summary>An empty database with no schema, for tests of migrating itself.</summary>
    public async Task<string> CreateEmptyDatabaseAsync()
    {
        var name = $"meridian_empty_{Interlocked.Increment(ref _counter)}";
        await ExecuteOnServerAsync($"CREATE DATABASE {name}");
        return ConnectionStringFor(name);
    }

    private async Task ExecuteOnServerAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionStringFor("postgres"));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
