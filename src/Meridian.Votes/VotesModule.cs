using Meridian.Data;
using Meridian.Platform.Contracts;
using Meridian.Platform.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Meridian.Votes;

/// <summary>HIS: vote search, the vote page and its timeline. Owned by slice 4.</summary>
public static class VotesModule
{
    public static IServiceCollection AddVotesModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapVotesModule(this IEndpointRouteBuilder api)
    {
        var votes = api.MapGroup("/votes").WithTags("Votes");

        votes.MapGet("", async Task<Results<Ok<PageDto<VoteSummaryDto>>, ProblemHttpResult>> (
                    MeridianDbContext db, CancellationToken ct,
                    string? q, int? session, DateOnly? from, DateOnly? to, bool noPosition = false,
                    int page = 1, int pageSize = 50) =>
                TypedResults.Ok(await VoteSearch.SearchAsync(db, new(q, session, from, to, noPosition), page, pageSize, ct)))
            .WithName("searchVotes")
            .WithSummary("Search by assembly vote id or title, session, date range, or votes with no position.")
            .ForAnalysts();

        votes.MapGet("/{voteId:long}", Results<Ok<VoteDto>, ProblemHttpResult> (long voteId) =>
                EndpointConventions.NotImplemented())
            .WithName("getVote")
            .ProducesProblems(StatusCodes.Status404NotFound)
            .ForAnalysts().Stub();

        votes.MapGet("/{voteId:long}/timeline", Results<Ok<TimelinePageDto>, ProblemHttpResult> (
                    long voteId, long? before, int pageSize = 50) =>
                EndpointConventions.NotImplemented())
            .WithName("getVoteTimeline")
            .WithSummary("The vote's timeline, newest first, paged by history id.")
            .ProducesProblems(StatusCodes.Status404NotFound)
            .ForAnalysts().Stub();

        return api;
    }
}
