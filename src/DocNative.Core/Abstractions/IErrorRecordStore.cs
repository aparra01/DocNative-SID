using DocNative.Core.Models;

namespace DocNative.Core.Abstractions;

public interface IErrorRecordStore
{
    Task<ErrorRecord> AddAsync(ErrorRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ErrorRecord>> GetRecordsForDateAsync(DateOnly date, CancellationToken cancellationToken = default);

    Task LoadPersistedRecordsAsync(DateOnly date, CancellationToken cancellationToken = default);
}
