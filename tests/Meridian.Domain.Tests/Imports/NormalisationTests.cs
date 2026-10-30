using Meridian.Domain.Imports;

namespace Meridian.Domain.Tests.Imports;

/// <summary>Map ticket 08 rule 2: documented normalisations, each recorded, and the comparison key.</summary>
public sealed class NormalisationTests
{
    private static readonly MemberStates Members = new(new Dictionary<string, IReadOnlyList<MembershipSpan>>
    {
        ["BRA"] = [new(new DateOnly(1990, 1, 1), null)],
        ["CIV"] = [new(new DateOnly(1990, 1, 1), null)],
    });

    private static NormalisedRow Normalise(string code = "BRA", string ballot = "Y", string date = "2024-01-10",
        string title = "Oceans", string session = "79", string vote = "MA/2024/001", string? name = "Brazil") =>
        RowNormaliser.Normalise(new ExportRow(2, "raw", 6, 6, new ExportFields(vote, session, date, title, code, ballot, name, null)), Members);

    [Fact]
    public void Rule2_a_clean_row_has_no_normalisations()
    {
        var row = Normalise();

        Assert.Null(row.Invalid);
        Assert.Empty(row.Normalisations);
        Assert.Equal(new VoteDetails(79, new DateOnly(2024, 1, 10), "Oceans"), row.Details);
        Assert.Equal(Ballot.Y, row.Ballot);
    }

    [Fact]
    public void Rule2_values_are_trimmed()
    {
        var row = Normalise(code: " BRA ", title: "  Oceans ");

        Assert.Equal("BRA", row.Values.StateCode);
        Assert.Equal("Oceans", row.Details!.Title);
        Assert.Equal(["country_code: trimmed", "resolution_title: trimmed"], row.Normalisations);
    }

    [Fact]
    public void Rule2_invisible_characters_are_removed_and_nbsp_becomes_a_space()
    {
        var row = Normalise(code: "BR​A", title: "Oceans and Seas\r", vote: "﻿MA/2024/001");

        Assert.Equal("BRA", row.Values.StateCode);
        Assert.Equal("Oceans and Seas", row.Details!.Title);
        Assert.Equal("MA/2024/001", row.Values.AssemblyVoteId);
        Assert.Contains("country_code: invisible characters removed", row.Normalisations);
        Assert.Contains("resolution_title: invisible characters removed", row.Normalisations);
        Assert.Contains("assembly_vote_id: invisible characters removed", row.Normalisations);
    }

    [Fact]
    public void Rule2_text_is_stored_in_unicode_nfc()
    {
        var row = Normalise(title: "Côte d'Ivoire");

        Assert.Equal("Côte d'Ivoire", row.Details!.Title);
        Assert.Equal(["resolution_title: Unicode NFC"], row.Normalisations);
    }

    [Fact]
    public void Rule2_country_codes_and_single_letter_ballots_are_upper_cased()
    {
        var row = Normalise(code: "bra", ballot: "a");

        Assert.Equal("BRA", row.Values.StateCode);
        Assert.Equal(Ballot.A, row.Ballot);
        Assert.Equal(["country_code: upper-cased", "ballot: upper-cased"], row.Normalisations);
    }

    [Theory]
    [InlineData("2024/02/21")]
    [InlineData("21-02-2024")]
    [InlineData("21/02/2024")]
    [InlineData("21.02.2024")]
    [InlineData("21/2/2024")]
    [InlineData("2024-2-21")]
    [InlineData("21 February 2024")]
    [InlineData("21 february 2024")]
    public void Rule2_day_first_dates_are_converted_to_iso(string date)
    {
        var row = Normalise(date: date);

        Assert.Null(row.Invalid);
        Assert.Equal(new DateOnly(2024, 2, 21), row.Details!.VoteDate);
        Assert.Equal([$"vote_date: {date} read as 2024-02-21"], row.Normalisations);
    }

    [Fact]
    public void Rule2_numeric_dates_are_always_read_day_first()
    {
        var row = Normalise(date: "03/04/2024");

        Assert.Equal(new DateOnly(2024, 4, 3), row.Details!.VoteDate);
    }

    [Theory]
    [InlineData("02/21/2024")]
    [InlineData("21/02/24")]
    [InlineData("2024-02-21T09:00:00Z")]
    [InlineData("2024-02-21 09:00")]
    [InlineData("2024-02-30")]
    [InlineData("31/04/2024")]
    [InlineData("21-02/2024")]
    [InlineData("Feb 21 2024")]
    [InlineData("tomorrow")]
    [InlineData("0000-01-01")]
    [InlineData("01/01/0000")]
    public void Rule2_ambiguous_or_impossible_dates_are_never_guessed(string date)
    {
        var row = Normalise(date: date);

        Assert.Equal(ReasonCode.InvalidDate, row.Invalid);
    }

    [Theory]
    [InlineData("Oceans  and   Seas", "oceans and seas")]
    [InlineData("OCEANS and seas", "oceans and seas")]
    [InlineData("“Blue Ocean” Declaration", "\"blue ocean\" declaration")]
    [InlineData("States’ Duties", "states' duties")]
    [InlineData("Follow‑up — Review", "follow-up - review")]
    [InlineData("Côte d'Ivoire", "côte d'ivoire")]
    public void Rule2_the_comparison_key_folds_case_spacing_quotes_and_dashes_but_keeps_accents(string title, string key)
    {
        Assert.Equal(key, RowNormaliser.ComparisonKey(title));
    }

    [Fact]
    public void Rule2_stored_titles_keep_their_case()
    {
        Assert.Equal("OCEANS and seas", Normalise(title: "OCEANS and seas").Details!.Title);
    }
}
