using System.Text;

namespace Meridian.Domain.Csv;

/// <summary>One physical record of a CSV file: its 1-based line number and fields.</summary>
public sealed record CsvRecord(int LineNo, string RawText, IReadOnlyList<string> Fields);

/// <summary>
/// Reads RFC 4180 CSV: quoted fields may hold commas and doubled quotes; a leading BOM
/// is dropped; LF and CRLF both end a record. A quoted field never spans lines in the
/// Assembly's files, so a record is one physical line.
/// </summary>
public static class CsvReader
{
    public static IEnumerable<CsvRecord> Read(TextReader reader)
    {
        var lineNo = 0;
        while (reader.ReadLine() is { } line)
        {
            lineNo++;
            if (lineNo == 1 && line.Length > 0 && line[0] == '﻿') line = line[1..];
            yield return new CsvRecord(lineNo, line, SplitFields(line));
        }
    }

    public static IReadOnlyList<string> SplitFields(string line)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c != '"') field.Append(c);
                else if (i + 1 < line.Length && line[i + 1] == '"') { field.Append('"'); i++; }
                else quoted = false;
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { fields.Add(field.ToString()); field.Clear(); }
            else field.Append(c);
        }
        fields.Add(field.ToString());
        return fields;
    }
}
