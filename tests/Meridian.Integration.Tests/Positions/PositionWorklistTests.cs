using System.Net.Http.Json;
using System.Text.Json;
using Meridian.Domain;
using Meridian.Integration.Tests.Infrastructure;

namespace Meridian.Integration.Tests.Positions;

/// <summary>POS-01: the worklist of votes with their stance and weight, votes without a position first.</summary>
public sealed class PositionWorklistTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Votes_without_a_position_come_first_then_by_date_and_assembly_vote_id()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        var positioned = await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10), title: "Ocean Treaty");
        await fx.VoteAsync("MA/2024/003", new DateOnly(2024, 2, 1));
        await fx.VoteAsync("MA/2024/002", new DateOnly(2024, 2, 1));
        await fx.PositionAsync(positioned, Stance.Yes, 4);

        var page = await api.ClientAs("dana").GetFromJsonAsync<JsonElement>("/api/positions", Ct);

        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["MA/2024/002", "MA/2024/003", "MA/2024/001"],
            items.Select(i => i.GetProperty("vote").GetProperty("assemblyVoteId").GetString()));
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("position").ValueKind);
        var withPosition = items[2];
        Assert.Equal(("Ocean Treaty", 79, "2024-01-10"),
            (withPosition.GetProperty("title").GetString(), withPosition.GetProperty("session").GetInt32(),
             withPosition.GetProperty("voteDate").GetString()));
        var position = withPosition.GetProperty("position");
        Assert.Equal(("yes", 4, "lena"),
            (position.GetProperty("stance").GetString(), position.GetProperty("weight").GetInt32(),
             position.GetProperty("setBy").GetString()));
        Assert.Equal(3, page.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task No_position_filter_returns_only_votes_without_a_position()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        await fx.PositionAsync(await fx.VoteAsync("MA/2024/001", new DateOnly(2024, 1, 10)), Stance.No, 2);
        await fx.VoteAsync("MA/2024/002", new DateOnly(2024, 1, 11));

        var page = await api.ClientAs("lena").GetFromJsonAsync<JsonElement>("/api/positions?noPosition=true", Ct);

        Assert.Equal(["MA/2024/002"], page.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("vote").GetProperty("assemblyVoteId").GetString()));
        Assert.Equal(1, page.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Pages_through_the_worklist_with_the_full_total()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);
        await using var fx = Meridian.Fixtures.Fixtures.For(api.ConnectionString);
        for (var i = 1; i <= 5; i++) await fx.VoteAsync($"MA/2024/00{i}", new DateOnly(2024, 1, i));

        var page = await api.ClientAs("dana").GetFromJsonAsync<JsonElement>("/api/positions?page=2&pageSize=2", Ct);

        Assert.Equal(["MA/2024/003", "MA/2024/004"], page.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("vote").GetProperty("assemblyVoteId").GetString()));
        Assert.Equal((5, 2, 2), (page.GetProperty("total").GetInt32(), page.GetProperty("page").GetInt32(),
            page.GetProperty("pageSize").GetInt32()));
    }
}
