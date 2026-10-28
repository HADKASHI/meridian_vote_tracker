using System.Net;
using Meridian.Integration.Tests.Infrastructure;

namespace Meridian.Integration.Tests.Api;

public sealed class HealthTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    [Fact]
    public async Task Ready_when_the_database_and_blob_storage_answer()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var client = api.ClientAs(null);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Live_but_not_ready_when_the_database_is_down()
    {
        await using var api = new ApiFactory("Host=127.0.0.1;Port=1;Database=none;Username=x;Password=x;Timeout=1", azurite.ConnectionString);
        var client = api.ClientAs(null);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready", TestContext.Current.CancellationToken)).StatusCode);
    }
}
