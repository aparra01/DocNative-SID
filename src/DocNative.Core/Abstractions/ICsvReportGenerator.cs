using DocNative.Core.Models;

namespace DocNative.Core.Abstractions;

public interface ICsvReportGenerator
{
    Task<string> GenerateAsync(DateOnly date, IReadOnlyList<ErrorRecord> records, string outputDirectory, CancellationToken cancellationToken = default);
}
