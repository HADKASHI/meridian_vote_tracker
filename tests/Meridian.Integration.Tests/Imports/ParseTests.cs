using Meridian.Domain;
using Meridian.Imports;
using Meridian.Integration.Tests.Infrastructure;
using Meridian.Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Integration.Tests.Imports;

/// <summary>Map ticket 08 rule 1 through the API: a file that can't be read is rejected and applies nothing.</summary>
public sealed class ParseTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    [Theory]
    [InlineData("rejected-missing-column.csv", "Missing required column: resolution_title.")]
    [InlineData("rejected-invalid-utf8.csv", "The file is not valid UTF-8.")]
    [InlineData("rejected-no-data-rows.csv", "The file has no data rows.")]
    public async Task A_rejected_file_records_its_reason_and_applies_nothing(string fixture, string reason)
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var import = await ImportApi.ImportFixtureAsync(api, fixture);

        Assert.Equal(JobState.Done, import.Job.State);
        Assert.Equal(reason, import.Parse!.RejectedReason);
        Assert.Equal(NoRows, import.Counts);
        var list = await ImportApi.GetAsync<PageDto<ImportSummaryDto>>(api.ClientAs("dana"), "/api/imports");
        Assert.Equal(reason, Assert.Single(list.Items).RejectedReason);
        await using var db = Db(api);
        Assert.Equal(0, await db.ImportRows.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await db.Votes.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_parse_records_the_header_bom_line_endings_and_an_extra_columns_warning()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var import = await ImportApi.ImportFixtureAsync(api, "extra-columns.csv");

        var parse = import.Parse!;
        Assert.Null(parse.RejectedReason);
        Assert.True(parse.HasBom);
        Assert.Equal(LineEndings.Crlf, parse.LineEndings);
        Assert.Equal(1, parse.DataRows);
        Assert.Equal(["assembly_vote_id", "session", "vote_date", "resolution_title", "country_code", "ballot", "region"], parse.Header);
        var warning = Assert.Single(import.Warnings);
        Assert.Equal(ImportWarningKind.ExtraColumns, warning.Kind);
        Assert.Equal(["region"], warning.Columns);
        Assert.Equal(1, import.Job.RowsRead);
    }

    private static readonly OutcomeCountsDto NoRows = new(0, 0, 0, 0, 0, 0);

    private static Data.MeridianDbContext Db(ApiFactory api) => new(Data.MeridianDb.Options(api.ConnectionString));
}
