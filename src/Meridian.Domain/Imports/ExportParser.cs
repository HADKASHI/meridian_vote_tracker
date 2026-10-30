using System.Globalization;
using System.Text;
using Meridian.Domain.Csv;

namespace Meridian.Domain.Imports;

public enum LineEnding { Lf, Crlf, Mixed }

/// <summary>A row's values by column, as written in the file (before normalisation).</summary>
public sealed record ExportFields(
    string AssemblyVoteId, string Session, string VoteDate, string Title, string CountryCode, string Ballot,
    string? CountryName, string? PublishedAt);

/// <summary>One data line. <see cref="Fields"/> is null when its field count differs from the header's.</summary>
public sealed record ExportRow(int LineNo, string RawText, int FieldCount, int HeaderFieldCount, ExportFields? Fields);

/// <summary>An export file as parsed. It is rejected, with no rows, when <see cref="RejectedReason"/> is set.</summary>
public sealed record ParsedExport(
    IReadOnlyList<string> Header, int HeaderFieldCount, bool HasBom, LineEnding LineEndings,
    IReadOnlyList<string> ExtraColumns, IReadOnlyList<ExportRow> Rows, DateTimeOffset? MaxPublishedAt,
    string? RejectedReason)
{
    public int DataRows => Rows.Count;
}

/// <summary>
/// Columns are matched by header name (trimmed, any case, any order);
/// extra columns are ignored and listed; a BOM is stripped and LF and CRLF both end a
/// line. A missing required column, invalid UTF-8 or no data rows rejects the whole file.
/// </summary>
public static class ExportParser
{
    public const string AssemblyVoteId = "assembly_vote_id";
    public const string Session = "session";
    public const string VoteDate = "vote_date";
    public const string Title = "resolution_title";
    public const string CountryCode = "country_code";
    public const string Ballot = "ballot";
    public const string CountryName = "country_name";
    public const string PublishedAt = "published_at";

    private static readonly string[] Required = [AssemblyVoteId, Session, VoteDate, Title, CountryCode, Ballot];
    private static readonly string[] Optional = [CountryName, PublishedAt];

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <param name="onLinesRead">Called every 10,000 lines with the count so far, for progress.</param>
    public static async Task<ParsedExport> ParseAsync(Stream content, Func<int, Task>? onLinesRead = null,
        CancellationToken cancellationToken = default)
    {
        List<(int LineNo, string Text)> lines;
        bool hasBom;
        LineEnding endings;
        try
        {
            (lines, hasBom, endings) = await ReadLinesAsync(content, onLinesRead, cancellationToken);
        }
        catch (DecoderFallbackException)
        {
            return Rejected([], false, LineEnding.Lf, "The file is not valid UTF-8.");
        }

        if (lines.Count == 0) return Rejected([], hasBom, endings, "The file is empty.");

        var header = CsvReader.SplitFields(lines[0].Text).Select(c => c.Trim()).ToList();
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Count; i++) columns.TryAdd(header[i], i);

        var missing = Required.Where(c => !columns.ContainsKey(c)).ToList();
        if (missing.Count > 0)
            return Rejected(header, hasBom, endings,
                $"Missing required column{(missing.Count > 1 ? "s" : "")}: {string.Join(", ", missing)}.");

        var dataLines = lines.Skip(1).Where(l => l.Text.Length > 0).ToList();
        if (dataLines.Count == 0) return Rejected(header, hasBom, endings, "The file has no data rows.");

        var known = Required.Concat(Optional).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var extra = header.Where(c => !known.Contains(c)).ToList();
        int? Column(string name) => columns.TryGetValue(name, out var i) ? i : null;
        var (vote, session, date, title, code, ballot) =
            (columns[AssemblyVoteId], columns[Session], columns[VoteDate], columns[Title], columns[CountryCode], columns[Ballot]);
        var (name, published) = (Column(CountryName), Column(PublishedAt));

        var rows = new List<ExportRow>(dataLines.Count);
        DateTimeOffset? maxPublished = null;
        foreach (var (lineNo, text) in dataLines)
        {
            var f = CsvReader.SplitFields(text);
            if (f.Count != header.Count)
            {
                rows.Add(new ExportRow(lineNo, text, f.Count, header.Count, null));
                continue;
            }
            var fields = new ExportFields(f[vote], f[session], f[date], f[title], f[code], f[ballot],
                name is { } n ? f[n] : null, published is { } p ? f[p] : null);
            rows.Add(new ExportRow(lineNo, text, f.Count, header.Count, fields));
            if (ParsePublishedAt(fields.PublishedAt) is { } at && (maxPublished is null || at > maxPublished)) maxPublished = at;
        }

        return new ParsedExport(header, header.Count, hasBom, endings, extra, rows, maxPublished, null);
    }

    /// <summary>An ISO 8601 timestamp; anything else is treated as absent, since it is never used to classify.</summary>
    public static DateTimeOffset? ParsePublishedAt(string? value) =>
        DateTimeOffset.TryParse(value?.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)
            ? at
            : null;

    private static ParsedExport Rejected(IReadOnlyList<string> header, bool hasBom, LineEnding endings, string reason) =>
        new(header, header.Count, hasBom, endings, [], [], null, reason);

    /// <summary>Splits on LF, dropping a CR before it, and notes which endings were seen.</summary>
    private static async Task<(List<(int, string)> Lines, bool HasBom, LineEnding Endings)> ReadLinesAsync(
        Stream content, Func<int, Task>? onLinesRead, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(content, StrictUtf8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var lines = new List<(int, string)>();
        var line = new StringBuilder();
        var buffer = new char[64 * 1024];
        var (lf, crlf, hasBom, first) = (false, false, false, true);
        int read;
        while ((read = await reader.ReadAsync(buffer, cancellationToken)) > 0)
        {
            var start = 0;
            if (first)
            {
                first = false;
                if (buffer[0] == '﻿') (hasBom, start) = (true, 1);
            }
            for (var i = start; i < read; i++)
            {
                if (buffer[i] != '\n')
                {
                    line.Append(buffer[i]);
                    continue;
                }
                if (line.Length > 0 && line[^1] == '\r')
                {
                    line.Length--;
                    crlf = true;
                }
                else lf = true;
                lines.Add((lines.Count + 1, line.ToString()));
                line.Clear();
                if (lines.Count % 10_000 == 0 && onLinesRead is not null) await onLinesRead(lines.Count);
            }
        }
        if (line.Length > 0) lines.Add((lines.Count + 1, line.ToString()));
        if (onLinesRead is not null) await onLinesRead(lines.Count);
        return (lines, hasBom, crlf && lf ? LineEnding.Mixed : crlf ? LineEnding.Crlf : LineEnding.Lf);
    }
}
