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

/// <summary>
/// A reader's failure, in words free of the sheet's content. <see cref="Permanent"/> means trying again can't help
/// (the service refused the file); otherwise it is worth retrying (a timeout, a busy service).
/// </summary>
public sealed class SheetReaderException(string message, bool permanent) : Exception(message)
{
    public bool Permanent => permanent;
}

/// <summary>Stands in until a real reader is configured, so a queued sheet fails with a clear reason and nothing crashes.</summary>
internal sealed class NotConfiguredSheetReader : IVoteSheetReader
{
    public Task<SheetRead> ReadAsync(Stream pdf, CancellationToken cancellationToken) =>
        throw new JobFailedException("No voting sheet reader is configured.");
}
