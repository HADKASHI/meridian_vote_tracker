using System.Net.Http.Json;
using System.Text.Json;
using Meridian.Integration.Tests.Infrastructure;

namespace Meridian.Integration.Tests.Api;

public sealed class IdentityTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    [Fact]
    public async Task The_switcher_lists_the_three_users_with_their_roles_before_anyone_is_chosen()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var users = await api.ClientAs(null).GetFromJsonAsync<JsonElement>("/api/users", TestContext.Current.CancellationToken);

        Assert.Equal(
            ["dana:Dana:analyst", "lena:Lena:admin", "omar:Omar:analyst"],
            users.EnumerateArray().Select(u => $"{u.GetProperty("id")}:{u.GetProperty("displayName")}:{u.GetProperty("role")}"));
    }

    [Fact]
    public async Task A_known_user_is_signed_in_with_their_role()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var me = await api.ClientAs("lena").GetFromJsonAsync<JsonElement>("/api/users/me", TestContext.Current.CancellationToken);

        Assert.Equal("lena", me.GetProperty("id").GetString());
        Assert.Equal("admin", me.GetProperty("role").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("mallory")]
    public async Task A_missing_or_unknown_user_gets_401_unknown_user(string? userId)
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var response = await api.ClientAs(userId).GetAsync("/api/users/me", TestContext.Current.CancellationToken);

        await response.AssertProblemAsync(401, "unknown-user");
    }

    [Fact]
    public async Task An_unknown_route_is_a_404_problem_with_a_trace_id()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var response = await api.ClientAs("dana").GetAsync("/api/no-such-thing", TestContext.Current.CancellationToken);

        await response.AssertProblemAsync(404, "not-found");
    }

    [Fact]
    public async Task An_unexpected_error_is_a_500_problem_with_a_trace_id_and_no_message()
    {
        await using var api = new ApiFactory(ApiFactory.UnreachableDatabase, azurite.ConnectionString);

        var response = await api.ClientAs(null).GetAsync("/api/users", TestContext.Current.CancellationToken);

        var problem = await response.AssertProblemAsync(500, "unexpected");
        Assert.False(problem.TryGetProperty("detail", out _), "an unexpected error must not leak its message");
    }
}
