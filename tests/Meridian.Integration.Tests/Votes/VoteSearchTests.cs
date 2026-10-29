using System.Net.Http.Json;
using System.Text.Json;
using Meridian.Domain;
using Meridian.Integration.Tests.Infrastructure;

namespace Meridian.Integration.Tests.Votes;

/// <summary>HIS-01, user story 65: find any vote by assembly vote id, title, session, date and "no position".</summary>
public sealed class VoteSearchTests(PostgresFixture postgres, AzuriteFixture azurite) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private ApiFactory _api = null!;

    public async ValueTask InitializeAsync()
    {
        _api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(_api.ConnectionString);
        var ocean = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 15), session: 79, title: "Ocean Treaty");
        await fx.VoteAsync("MA/2024/002", new DateOnly(2024, 2, 10), session: 79, title: "Outer Space Accord");
        await fx.VoteAsync("MA/2023/014", new DateOnly(2023, 11, 3), session: 78, title: "Ocean Acidification Report");
        await fx.PositionAsync(ocean, Stance.Yes, 4);
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    private async Task<(string[] Ids, int Total)> SearchAsync(string query)
    {
        var page = await _api.ClientAs("omar").GetFromJsonAsync<JsonElement>($"/api/votes{query}", Ct);
        return (page.GetProperty("items").EnumerateArray().Select(v => v.GetProperty("assemblyVoteId").GetString()!).ToArray(),
            page.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task With_no_filter_every_vote_is_listed_newest_first()
    {
        var (ids, total) = await SearchAsync("");
        Assert.Equal(["MA/2024/002", "MA/2024/001", "MA/2023/014"], ids);
        Assert.Equal(3, total);
    }

    [Fact]
    public async Task The_search_box_matches_part_of_the_assembly_vote_id_or_title_ignoring_case()
    {
        Assert.Equal(["MA/2024/001", "MA/2023/014"], (await SearchAsync("?q=ocean")).Ids);
        Assert.Equal(["MA/2023/014"], (await SearchAsync("?q=2023/0")).Ids);
    }

    [Fact]
    public async Task Session_filters_on_the_approved_session()
    {
        Assert.Equal(["MA/2023/014"], (await SearchAsync("?session=78")).Ids);
    }

    [Fact]
    public async Task A_date_range_is_inclusive_at_both_ends()
    {
        Assert.Equal(["MA/2024/002", "MA/2024/001"], (await SearchAsync("?from=2024-01-15&to=2024-02-10")).Ids);
        Assert.Equal(["MA/2023/014"], (await SearchAsync("?to=2024-01-14")).Ids);
    }

    [Fact]
    public async Task No_position_lists_only_votes_without_a_position()
    {
        Assert.Equal(["MA/2024/002", "MA/2023/014"], (await SearchAsync("?noPosition=true")).Ids);
    }

    [Fact]
    public async Task Filters_combine()
    {
        Assert.Equal(["MA/2024/001"], (await SearchAsync("?q=ocean&session=79")).Ids);
        Assert.Empty((await SearchAsync("?q=ocean&session=79&noPosition=true")).Ids);
    }

    [Fact]
    public async Task Results_are_paged_with_the_total()
    {
        var (ids, total) = await SearchAsync("?page=2&pageSize=1");
        Assert.Equal(["MA/2024/001"], ids);
        Assert.Equal(3, total);
    }

    [Fact]
    public async Task Pages_keep_the_order_with_and_without_a_position()
    {
        var pages = new List<string>();
        for (var page = 1; page <= 3; page++) pages.AddRange((await SearchAsync($"?page={page}&pageSize=1")).Ids);

        Assert.Equal(["MA/2024/002", "MA/2024/001", "MA/2023/014"], pages);
    }

    [Fact]
    public async Task A_result_carries_the_vote_details_and_its_position()
    {
        var page = await _api.ClientAs("dana").GetFromJsonAsync<JsonElement>("/api/votes?q=MA/2024/001", Ct);

        var vote = page.GetProperty("items")[0];
        Assert.Equal((79, "2024-01-15", "Ocean Treaty"),
            (vote.GetProperty("session").GetInt32(), vote.GetProperty("voteDate").GetString(), vote.GetProperty("title").GetString()));
        var position = vote.GetProperty("position");
        Assert.Equal(("yes", 4, "lena"),
            (position.GetProperty("stance").GetString(), position.GetProperty("weight").GetInt32(), position.GetProperty("setBy").GetString()));
        Assert.True(vote.GetProperty("id").GetInt64() > 0);
    }

    [Fact]
    public async Task A_vote_with_no_position_has_none()
    {
        var page = await _api.ClientAs("dana").GetFromJsonAsync<JsonElement>("/api/votes?q=MA/2024/002", Ct);

        Assert.Equal(JsonValueKind.Null, page.GetProperty("items")[0].GetProperty("position").ValueKind);
    }
}
