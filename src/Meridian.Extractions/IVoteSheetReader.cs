using Meridian.Domain.Sheets;
using Meridian.Platform.Jobs;

namespace Meridian.Extractions;

/// <summary>
/// Reads a scanned voting sheet: its text lines and its table cells, each with the reader's confidence
/// (ADR 0008). The one place an AI service is called; tests give the job a fake one.
/// </summary>
public interface IVoteSheetReader
{
    Task<SheetRead> ReadAsync(Stream pdf, CancellationToken cancellationToken);
}

/// <summary>Stands in until a real reader is configured, so a queued sheet fails with a clear reason and nothing crashes.</summary>
internal sealed class NotConfiguredSheetReader : IVoteSheetReader
{
    public Task<SheetRead> ReadAsync(Stream pdf, CancellationToken cancellationToken) =>
        throw new JobFailedException("No voting sheet reader is configured.");
}
