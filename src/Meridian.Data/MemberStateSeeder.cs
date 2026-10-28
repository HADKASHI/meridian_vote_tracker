using System.Globalization;
using System.Text;
using Meridian.Data.Entities;
using Meridian.Domain.Csv;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Data;

/// <summary>
/// Loads member states and their membership periods from <c>Seed:MemberStatesPath</c>
/// when <c>member_state</c> is empty. The file is assignment data, so it is never in a
/// migration; without it the system can't classify anything, so a missing file fails
/// the migration loudly.
/// </summary>
public static class MemberStateSeeder
{
    private static readonly string[] Columns = ["country_code", "country_name", "member_from", "member_to"];

    public static void Seed(MeridianDbContext db, string? path)
    {
        if (db.MemberStates.Any()) return;
        var (states, periods) = Load(path);
        db.MemberStates.AddRange(states);
        db.MembershipPeriods.AddRange(periods);
        db.SaveChanges();
    }

    public static async Task SeedAsync(MeridianDbContext db, string? path, CancellationToken cancellationToken)
    {
        if (await db.MemberStates.AnyAsync(cancellationToken)) return;
        var (states, periods) = Load(path);
        db.MemberStates.AddRange(states);
        db.MembershipPeriods.AddRange(periods);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static (List<MemberState>, List<MembershipPeriod>) Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException(
                "Seed:MemberStatesPath is not set, so the member states can't be loaded. Point it at member_states.csv.");
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"The member states file '{Path.GetFileName(path)}' was not found in '{Path.GetDirectoryName(Path.GetFullPath(path))}'. " +
                "Put the assignment's member_states.csv in data_candidate/ at the repository root and run again.", path);

        using var reader = new StreamReader(path, new UTF8Encoding(false, throwOnInvalidBytes: true));
        using var records = CsvReader.Read(reader).GetEnumerator();
        if (!records.MoveNext()) throw Invalid(path, 1, "the file is empty");

        var header = records.Current.Fields.Select(f => f.Trim().ToLowerInvariant()).ToList();
        var index = Columns.ToDictionary(c => c, c => header.IndexOf(c));
        if (index.FirstOrDefault(c => c.Value < 0) is { Key: { } missing })
            throw Invalid(path, 1, $"the column '{missing}' is missing");

        var states = new Dictionary<string, MemberState>();
        var periods = new List<MembershipPeriod>();
        while (records.MoveNext())
        {
            var record = records.Current;
            if (record.RawText.Length == 0) continue;
            if (record.Fields.Count != header.Count) throw Invalid(path, record.LineNo, "wrong number of fields");
            string Field(string column) => record.Fields[index[column]].Trim();

            var code = Field("country_code").ToUpperInvariant();
            states.TryAdd(code, new MemberState { Code = code, Name = Field("country_name") });
            periods.Add(new MembershipPeriod
            {
                StateCode = code,
                MemberFrom = ParseDate(path, record.LineNo, Field("member_from")),
                MemberTo = Field("member_to") is "" ? null : ParseDate(path, record.LineNo, Field("member_to")),
            });
        }

        CheckNoOverlaps(path, periods);
        return (states.Values.ToList(), periods);
    }

    // Overlaps are checked here rather than by a database constraint (docs/schema.md).
    private static void CheckNoOverlaps(string path, List<MembershipPeriod> periods)
    {
        foreach (var state in periods.GroupBy(p => p.StateCode))
        {
            var ordered = state.OrderBy(p => p.MemberFrom).ToList();
            for (var i = 1; i < ordered.Count; i++)
                if (ordered[i - 1].MemberTo is not { } end || end >= ordered[i].MemberFrom)
                    throw new InvalidDataException($"{path}: membership periods of {state.Key} overlap.");
        }
    }

    private static DateOnly ParseDate(string path, int lineNo, string value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw Invalid(path, lineNo, $"'{value}' is not a yyyy-mm-dd date");

    private static InvalidDataException Invalid(string path, int lineNo, string problem) =>
        new($"{path}, line {lineNo}: {problem}.");
}
