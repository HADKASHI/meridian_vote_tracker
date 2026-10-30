using System.Globalization;
using Meridian.Domain.Imports;

namespace Meridian.Domain.Sheets;

/// <summary>
/// An approved sheet as the rows of a file (PDF-05): each grid row becomes the export row a CSV would have carried
/// for it, so the importer classifies and writes it with its own rules and not a second implementation. The line
/// number is the row's position on the sheet.
/// </summary>
public static class SheetRows
{
    private const int Fields = 6;

    /// <param name="details">The vote's session, date and title as the checked sheet reads them.</param>
    public static IReadOnlyList<ExportRow> ToExportRows(MappedSheet sheet, VoteDetails details) =>
        sheet.Rows.Select(row => new ExportRow(
            row.Position,
            $"{row.Code} {row.Name}: {row.Choice}",
            Fields, Fields,
            new ExportFields(
                sheet.Header.VoteId!.Trim(),
                details.Session.ToString(CultureInfo.InvariantCulture),
                details.VoteDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                details.Title,
                row.Code,
                row.Choice!.Value.ToString(),
                row.Name,
                PublishedAt: null))).ToList();
}
