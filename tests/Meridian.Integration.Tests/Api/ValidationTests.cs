using System.Net.Http.Json;
using System.Text;
using Meridian.Integration.Tests.Infrastructure;

namespace Meridian.Integration.Tests.Api;

public sealed class ValidationTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    [Fact]
    public async Task Validation_errors_are_keyed_by_camel_case_json_paths()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        var body = """{"name":"","note":null,"selection":{"from":"2024-03-31","to":"2024-01-01"}}""";

        var response = await api.ClientAs("dana").PostAsync("/api/snapshots",
            new StringContent(body, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        var problem = await response.AssertProblemAsync(400, "validation");
        var keys = problem.GetProperty("errors").EnumerateObject().Select(e => e.Name).Order().ToList();
        Assert.Equal(["name", "selection.to"], keys);
    }

    [Fact]
    public async Task Nested_list_items_are_keyed_with_their_index()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var response = await api.ClientAs("dana").PostAsJsonAsync("/api/snapshots/preview",
            new { sessions = new[] { 79, 0 } }, TestContext.Current.CancellationToken);

        var problem = await response.AssertProblemAsync(400, "validation");
        Assert.Equal(["sessions[1]"], problem.GetProperty("errors").EnumerateObject().Select(e => e.Name));
    }

    [Theory]
    [InlineData("/api/votes?page=0", "page")]
    [InlineData("/api/votes?page=-3", "page")]
    [InlineData("/api/votes?page=2147483647", "page")]
    [InlineData("/api/votes?pageSize=0", "pageSize")]
    [InlineData("/api/votes?pageSize=201", "pageSize")]
    [InlineData("/api/votes?from=2024-03-31&to=2024-01-01", "from")]
    [InlineData("/api/votes/1/timeline?pageSize=0", "pageSize")]
    [InlineData("/api/votes/1/timeline?pageSize=1000", "pageSize")]
    [InlineData("/api/votes/1/timeline?before=0", "before")]
    [InlineData("/api/positions?page=0", "page")]
    [InlineData("/api/positions?page=2147483647", "page")]
    [InlineData("/api/positions?pageSize=0", "pageSize")]
    [InlineData("/api/positions?pageSize=201", "pageSize")]
    public async Task Invalid_paging_and_date_query_parameters_are_rejected_with_400(string url, string key)
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var response = await api.ClientAs("dana").GetAsync(url, TestContext.Current.CancellationToken);

        var problem = await response.AssertProblemAsync(400, "validation");
        Assert.Contains(key, problem.GetProperty("errors").EnumerateObject().Select(e => e.Name));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task A_non_positive_seen_revision_id_is_rejected_with_400(long seen)
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var response = await api.ClientAs("lena").PutAsJsonAsync("/api/votes/1/position",
            new { stance = "yes", weight = 3, seenRevisionId = seen }, TestContext.Current.CancellationToken);

        var problem = await response.AssertProblemAsync(400, "validation");
        Assert.Equal(["seenRevisionId"], problem.GetProperty("errors").EnumerateObject().Select(e => e.Name));
    }
}
