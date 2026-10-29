using System.Text;
using Meridian.Domain.Imports;

namespace Meridian.Domain.Tests.Imports;

/// <summary>Map ticket 08 rule 1: parsing is header-driven, and a file that can't be read is rejected.</summary>
public sealed class ExportParserTests
{
    private const string Header = "assembly_vote_id,session,vote_date,resolution_title,country_code,ballot";

    private static Task<ParsedExport> Parse(string text) => ExportParser.ParseAsync(new MemoryStream(Encoding.UTF8.GetBytes(text)));

    private static Task<ParsedExport> Parse(byte[] bytes) => ExportParser.ParseAsync(new MemoryStream(bytes));

    [Fact]
    public async Task Rule1_columns_are_matched_by_name_in_any_order_and_case()
    {
        var parsed = await Parse(" Ballot ,COUNTRY_CODE,Resolution_Title,vote_date,session,assembly_vote_id\nY,BRA,\"Oceans, Seas\",2024-01-10,79,MA/2024/001\n");

        Assert.Null(parsed.RejectedReason);
        var row = Assert.Single(parsed.Rows);
        Assert.Equal(new ExportFields("MA/2024/001", "79", "2024-01-10", "Oceans, Seas", "BRA", "Y", null, null), row.Fields);
        Assert.Equal(2, row.LineNo);
        Assert.Equal("Y,BRA,\"Oceans, Seas\",2024-01-10,79,MA/2024/001", row.RawText);
    }

    [Fact]
    public async Task Rule1_the_title_column_is_resolution_title_and_a_plain_title_column_is_not_enough()
    {
        var parsed = await Parse("assembly_vote_id,session,vote_date,title,country_code,ballot\nMA/2024/001,79,2024-01-10,A,BRA,Y\n");

        Assert.Equal("Missing required column: resolution_title.", parsed.RejectedReason);
        Assert.Empty(parsed.Rows);
    }

    [Fact]
    public async Task Rule1_optional_columns_are_read_when_present()
    {
        var parsed = await Parse($"{Header},country_name,published_at\nMA/2024/001,79,2024-01-10,A,BRA,Y,Brazil,2024-04-02T09:00:00Z\n");

        var fields = Assert.Single(parsed.Rows).Fields!;
        Assert.Equal("Brazil", fields.CountryName);
        Assert.Equal("2024-04-02T09:00:00Z", fields.PublishedAt);
        Assert.Equal(DateTimeOffset.Parse("2024-04-02T09:00:00Z"), parsed.MaxPublishedAt);
    }

    [Fact]
    public async Task Rule1_the_newest_published_at_is_recorded()
    {
        var parsed = await Parse($"{Header},published_at\nMA/1,79,2024-01-10,A,BRA,Y,2024-04-02T09:00:00Z\nMA/1,79,2024-01-10,A,FRA,Y,2024-04-05T09:00:00Z\nMA/1,79,2024-01-10,A,JPN,Y,\n");

        Assert.Equal(DateTimeOffset.Parse("2024-04-05T09:00:00Z"), parsed.MaxPublishedAt);
    }

    [Fact]
    public async Task Rule1_extra_columns_are_ignored_and_listed()
    {
        var parsed = await Parse($"{Header},region,Notes\nMA/2024/001,79,2024-01-10,A,BRA,Y,Americas,x\n");

        Assert.Null(parsed.RejectedReason);
        Assert.Equal(["region", "Notes"], parsed.ExtraColumns);
        Assert.Equal("Y", Assert.Single(parsed.Rows).Fields!.Ballot);
    }

    [Fact]
    public async Task Rule1_the_header_is_kept_as_found()
    {
        var parsed = await Parse($"{Header.ToUpperInvariant()},Region\nMA/1,79,2024-01-10,A,BRA,Y,x\n");

        Assert.Equal(["ASSEMBLY_VOTE_ID", "SESSION", "VOTE_DATE", "RESOLUTION_TITLE", "COUNTRY_CODE", "BALLOT", "Region"], parsed.Header);
    }

    [Fact]
    public async Task Rule1_a_bom_is_stripped_and_recorded()
    {
        var withBom = await Parse(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes($"{Header}\nMA/1,79,2024-01-10,A,BRA,Y\n")).ToArray());
        var without = await Parse($"{Header}\nMA/1,79,2024-01-10,A,BRA,Y\n");

        Assert.True(withBom.HasBom);
        Assert.False(without.HasBom);
        Assert.Null(withBom.RejectedReason);
        Assert.Equal("assembly_vote_id", withBom.Header[0]);
    }

    [Theory]
    [InlineData("\n", "\n", LineEnding.Lf)]
    [InlineData("\r\n", "\r\n", LineEnding.Crlf)]
    [InlineData("\r\n", "\n", LineEnding.Mixed)]
    public async Task Rule1_lf_and_crlf_are_accepted_and_recorded(string first, string second, LineEnding expected)
    {
        var parsed = await Parse($"{Header}{first}MA/1,79,2024-01-10,A,BRA,Y{second}MA/1,79,2024-01-10,A,FRA,N{second}");

        Assert.Equal(expected, parsed.LineEndings);
        Assert.Equal(["Y", "N"], parsed.Rows.Select(r => r.Fields!.Ballot));
        Assert.Equal("MA/1,79,2024-01-10,A,BRA,Y", parsed.Rows[0].RawText);
    }

    [Fact]
    public async Task Rule1_the_last_line_may_end_without_a_line_break()
    {
        var parsed = await Parse($"{Header}\nMA/1,79,2024-01-10,A,BRA,Y");

        Assert.Equal(1, parsed.DataRows);
        Assert.Equal(LineEnding.Lf, parsed.LineEndings);
    }

    [Fact]
    public async Task Rule1_a_row_whose_field_count_differs_from_the_header_has_no_fields()
    {
        var parsed = await Parse($"{Header}\nMA/1;79,2024-01-10,A,BRA\nMA/1,79,2024-01-10,A,BRA,Y,extra\n");

        Assert.All(parsed.Rows, r => Assert.Null(r.Fields));
        Assert.Equal([4, 7], parsed.Rows.Select(r => r.FieldCount));
        Assert.Equal(6, parsed.HeaderFieldCount);
        Assert.Equal(2, parsed.DataRows);
    }

    [Fact]
    public async Task Rule1_empty_lines_are_not_rows()
    {
        var parsed = await Parse($"{Header}\n\nMA/1,79,2024-01-10,A,BRA,Y\n\n");

        var row = Assert.Single(parsed.Rows);
        Assert.Equal(3, row.LineNo);
    }

    [Fact]
    public async Task Rule1_a_missing_required_column_rejects_the_file()
    {
        var parsed = await Parse("assembly_vote_id,vote_date,resolution_title,country_code\nMA/1,2024-01-10,A,BRA\n");

        Assert.Equal("Missing required columns: session, ballot.", parsed.RejectedReason);
        Assert.Empty(parsed.Rows);
    }

    [Fact]
    public async Task Rule1_invalid_utf8_rejects_the_file()
    {
        var bytes = Encoding.UTF8.GetBytes($"{Header}\nMA/1,79,2024-01-10,A,BRA,Y\n").Concat(new byte[] { 0xC3, 0x28, 0x0A }).ToArray();

        var parsed = await Parse(bytes);

        Assert.Equal("The file is not valid UTF-8.", parsed.RejectedReason);
        Assert.Empty(parsed.Rows);
    }

    [Theory]
    [InlineData("")]
    [InlineData(Header + "\n")]
    [InlineData(Header + "\r\n\r\n")]
    public async Task Rule1_a_file_with_no_data_rows_is_rejected(string text)
    {
        var parsed = await Parse(text);

        Assert.Equal(text.Length == 0 ? "The file is empty." : "The file has no data rows.", parsed.RejectedReason);
        Assert.Empty(parsed.Rows);
    }
}
