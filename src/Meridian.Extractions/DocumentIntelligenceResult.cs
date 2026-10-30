using System.Text.Json;
using Meridian.Domain.Sheets;

namespace Meridian.Extractions;

/// <summary>
/// Document Intelligence's <c>analyzeResult</c> as the mapper's input: each page's text lines, and each table as
/// rows of cells. A cell's confidence is its least certain word's, found through the character spans the service
/// reports for the cell and for each word; a cell with no words (blank) is certain of being blank.
/// </summary>
internal static class DocumentIntelligenceResult
{
    public static SheetRead ToSheetRead(JsonElement analyzeResult)
    {
        var words = Words(analyzeResult);
        var tablesByPage = new Dictionary<int, List<SheetTable>>();
        if (analyzeResult.TryGetProperty("tables", out var tables))
            foreach (var table in tables.EnumerateArray())
            {
                var page = table.TryGetProperty("boundingRegions", out var regions) && regions.GetArrayLength() > 0
                    ? regions[0].GetProperty("pageNumber").GetInt32()
                    : 1;
                if (!tablesByPage.TryGetValue(page, out var onPage)) tablesByPage[page] = onPage = [];
                onPage.Add(ToTable(table, words));
            }

        var pages = new List<SheetPage>();
        if (analyzeResult.TryGetProperty("pages", out var found))
            foreach (var page in found.EnumerateArray())
            {
                var number = page.GetProperty("pageNumber").GetInt32();
                var lines = page.TryGetProperty("lines", out var l)
                    ? l.EnumerateArray().Select(line => line.GetProperty("content").GetString() ?? "").ToList()
                    : [];
                pages.Add(new SheetPage(lines, tablesByPage.GetValueOrDefault(number, [])));
            }
        return new SheetRead(pages);
    }

    private static SheetTable ToTable(JsonElement table, IReadOnlyList<(int Offset, int Length, double Confidence)> words)
    {
        var columns = table.GetProperty("columnCount").GetInt32();
        var rows = table.GetProperty("rowCount").GetInt32();
        var grid = new SheetCell?[rows][];
        for (var r = 0; r < rows; r++) grid[r] = new SheetCell?[columns];

        foreach (var cell in table.GetProperty("cells").EnumerateArray())
        {
            var (row, column) = (cell.GetProperty("rowIndex").GetInt32(), cell.GetProperty("columnIndex").GetInt32());
            if (row >= rows || column >= columns) continue;
            var spans = cell.TryGetProperty("spans", out var s)
                ? s.EnumerateArray().Select(x => (Offset: x.GetProperty("offset").GetInt32(), Length: x.GetProperty("length").GetInt32())).ToList()
                : [];
            var inside = words.Where(w => spans.Any(sp => w.Offset >= sp.Offset && w.Offset < sp.Offset + sp.Length)).ToList();
            grid[row][column] = new SheetCell(cell.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "",
                inside.Count == 0 ? 1.0 : inside.Min(w => w.Confidence));
        }
        // A merged or missing cell is an empty one, so every row has every column.
        return new SheetTable(grid.Select(r => (IReadOnlyList<SheetCell>)r.Select(c => c ?? new SheetCell("", 1.0)).ToList()).ToList());
    }

    private static List<(int Offset, int Length, double Confidence)> Words(JsonElement analyzeResult)
    {
        var words = new List<(int, int, double)>();
        if (analyzeResult.TryGetProperty("pages", out var pages))
            foreach (var page in pages.EnumerateArray())
                if (page.TryGetProperty("words", out var found))
                    foreach (var word in found.EnumerateArray())
                        words.Add((word.GetProperty("span").GetProperty("offset").GetInt32(),
                            word.GetProperty("span").GetProperty("length").GetInt32(),
                            word.GetProperty("confidence").GetDouble()));
        return words;
    }
}
