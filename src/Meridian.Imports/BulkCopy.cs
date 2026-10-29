using Meridian.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using NpgsqlTypes;
using Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.Mapping;

namespace Meridian.Imports;

/// <summary>
/// Inserts entities with PostgreSQL binary COPY on the context's connection and current
/// transaction. Table, column, type and value conversions come from the EF model, so the
/// schema stays defined in one place; a column with no explicit PostgreSQL type is written
/// as its CLR type says. Columns the database fills in (<c>recorded_at</c>)
/// are left out of the COPY.
/// </summary>
internal static class BulkCopy
{
    public static async Task InsertAsync<T>(MeridianDbContext db, IReadOnlyCollection<T> rows, CancellationToken ct) where T : class
    {
        if (rows.Count == 0) return;
        var entity = db.Model.FindEntityType(typeof(T))!;
        var table = StoreObjectIdentifier.Create(entity, StoreObjectType.Table)!.Value;
        var columns = entity.GetProperties().Where(p => p.Name != "RecordedAt")
            .Select(p => (Getter: p.GetGetter(), Converter: p.GetRelationalTypeMapping().Converter,
                Type: (p.GetRelationalTypeMapping() as NpgsqlTypeMapping)?.NpgsqlDbType,
                Name: p.GetColumnName(table)))
            .ToList();

        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var names = string.Join(", ", columns.Select(c => $"\"{c.Name}\""));
        await using var importer = await connection.BeginBinaryImportAsync(
            $"COPY \"{table.Name}\" ({names}) FROM STDIN (FORMAT BINARY)", ct);
        foreach (var row in rows)
        {
            await importer.StartRowAsync(ct);
            foreach (var column in columns)
            {
                var value = column.Getter.GetClrValue(row);
                if (value is not null && column.Converter is { } converter) value = converter.ConvertToProvider(value);
                if (value is null) await importer.WriteNullAsync(ct);
                else if (column.Type is { } type) await importer.WriteAsync(value, type, ct);
                else await importer.WriteAsync(value, ct);
            }
        }
        await importer.CompleteAsync(ct);
    }
}
