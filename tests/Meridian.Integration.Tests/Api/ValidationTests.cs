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
}
