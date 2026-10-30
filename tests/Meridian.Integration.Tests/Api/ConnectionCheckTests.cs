using Meridian.Integration.Tests.Infrastructure;
using Meridian.Platform.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Meridian.Integration.Tests.Api;

/// <summary>ADR 0009: running on Azure needs TLS to the database, and a connection that fails is named, never leaked.</summary>
public sealed class ConnectionCheckTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string DeadDatabase = "Host=db.example.invalid;Port=5432;Database=meridian;Username=app;Password=hunter2-db;Timeout=2";
    private const string DeadBlob = "DefaultEndpointsProtocol=https;AccountName=nosuchaccount;AccountKey=c2VjcmV0LWJsb2Ita2V5;EndpointSuffix=invalid";

    [Fact]
    public async Task Reachable_services_pass_without_TLS_when_it_is_not_required()
    {
        var failures = await ConnectionCheck.RunAsync(await postgres.CreateDatabaseAsync(), azurite.ConnectionString, requireTls: false, Ct);

        Assert.Empty(failures);
    }

    [Fact]
    public async Task An_unreachable_database_is_named_with_its_host_and_never_its_password()
    {
        var failures = await ConnectionCheck.RunAsync(DeadDatabase, azurite.ConnectionString, requireTls: true, Ct);

        var failure = Assert.Single(failures);
        Assert.Equal("database", failure.Service);
        Assert.Contains("db.example.invalid", failure.Message);
        Assert.DoesNotContain("hunter2-db", failure.Message);
    }

    [Fact]
    public async Task A_connection_string_without_TLS_is_refused_when_TLS_is_required()
    {
        var plain = new NpgsqlConnectionStringBuilder(await postgres.CreateDatabaseAsync()) { SslMode = SslMode.Disable }.ConnectionString;

        var failure = Assert.Single(await ConnectionCheck.RunAsync(plain, azurite.ConnectionString, requireTls: true, Ct));

        Assert.Equal("database", failure.Service);
        Assert.Contains("TLS", failure.Message);
    }

    [Fact]
    public async Task An_unreachable_blob_account_is_named_without_its_key()
    {
        var failures = await ConnectionCheck.RunAsync(await postgres.CreateDatabaseAsync(), DeadBlob, requireTls: false, Ct);

        var failure = Assert.Single(failures);
        Assert.Equal("blob", failure.Service);
        Assert.Contains("nosuchaccount", failure.Message);
        Assert.DoesNotContain("c2VjcmV0LWJsb2Ita2V5", failure.Message);
    }

    [Fact]
    public async Task Both_failures_are_reported_together()
    {
        var failures = await ConnectionCheck.RunAsync(DeadDatabase, DeadBlob, requireTls: false, Ct);

        Assert.Equal(["blob", "database"], failures.Select(f => f.Service).Order());
    }

    [Fact]
    public async Task The_api_logs_each_failed_connection_at_startup_when_asked_to()
    {
        var logs = new List<string>();
        await using var api = new ApiFactory(DeadDatabase, DeadBlob, new Dictionary<string, string?> { ["Startup:CheckConnections"] = "true" });
        await using var logged = api.WithWebHostBuilder(host => host.ConfigureLogging(l => l.AddProvider(new ListProvider(logs))));

        using var client = logged.CreateClient();

        await vi_WaitAsync(() => logs.Any(l => l.Contains("database")) && logs.Any(l => l.Contains("blob")));
        Assert.DoesNotContain(logs, l => l.Contains("hunter2-db") || l.Contains("c2VjcmV0LWJsb2Ita2V5"));
    }

    private static async Task vi_WaitAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++) await Task.Delay(100, Ct);
        Assert.True(condition(), "The startup message never came.");
    }

    private sealed class ListProvider(List<string> messages) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new L(messages);

        public void Dispose() { }

        private sealed class L(List<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                lock (messages) messages.Add(formatter(state, exception));
            }
        }
    }
}
