using Meridian.Data;
using Meridian.Platform.Contracts;
using Meridian.Platform.Http;
using Meridian.Platform.Problems;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Meridian.Votes;

/// <summary>Vote search, the vote page and its timeline.</summary>
public static class VotesModule
{
    public static IServiceCollection AddVotesModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapVotesModule(this IEndpointRouteBuilder api)
    {
        var votes = api.MapGroup("/votes").WithTags("Votes");

        votes.MapGet("", async Task<Results<Ok<PageDto<VoteSummaryDto>>, ProblemHttpResult>> (
                    MeridianDbContext db, CancellationToken ct, [AsParameters] VoteSearchQuery query) =>
                TypedResults.Ok(await VoteSearch.SearchAsync(db,
                    new(query.Q, query.Session, query.From, query.To, query.NoPosition ?? false),
                    query.EffectivePage, query.EffectivePageSize, ct)))
            .WithName("searchVotes")
            .ProducesProblems(StatusCodes.Status400BadRequest)
            .WithSummary("Search by assembly vote id or title, session, date range, or votes with no position.")
            .ForAnalysts();

        votes.MapGet("/{voteId:long}", async Task<Results<Ok<VoteDto>, ProblemHttpResult>> (
                    long voteId, MeridianDbContext db, CancellationToken ct) =>
                await VotePage.LoadAsync(db, voteId, ct) is { } vote ? TypedResults.Ok(vote) : VoteNotFound(voteId))
            .WithName("getVote")
            .ProducesProblems(StatusCodes.Status404NotFound)
            .ForAnalysts();

        votes.MapGet("/{voteId:long}/timeline", async Task<Results<Ok<TimelinePageDto>, ProblemHttpResult>> (
                    long voteId, MeridianDbContext db, CancellationToken ct, [AsParameters] TimelineQuery query) =>
                await VoteTimeline.LoadAsync(db, voteId, query.Before, query.EffectivePageSize, ct) is { } page
                    ? TypedResults.Ok(page)
                    : VoteNotFound(voteId))
            .WithName("getVoteTimeline")
            .WithSummary("The vote's timeline, newest first, paged by history id.")
            .ProducesProblems(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound)
            .ForAnalysts();

        return api;
    }

    private static ProblemHttpResult VoteNotFound(long voteId) =>
        ProblemDetailsSetup.NotFound("vote", voteId);
}
