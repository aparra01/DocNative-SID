using DocNative.Core.Models;

namespace DocNative.Core.Abstractions;

public interface IErrorRecordStore
{
    Task AppendErrorAsync(ErrorRecord record, CancellationToken cancellationToken = default);
}
