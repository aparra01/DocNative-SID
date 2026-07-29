using System.Collections.Concurrent;
using System.Text.Json;
using DocNative.Core.Abstractions;
using DocNative.Core.Models;
using Microsoft.Extensions.Logging;

namespace DocNative.Core.Errors;

public sealed class ErrorRecordStore : IErrorRecordStore
{
    private readonly ConcurrentDictionary<DateOnly, ConcurrentBag<ErrorRecord>> _records = new();
    private readonly ConcurrentDictionary<DateOnly, int> _counters = new();
    private readonly IPathLayout _pathLayout;
    private readonly ILogger<ErrorRecordStore> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public ErrorRecordStore(IPathLayout pathLayout, ILogger<ErrorRecordStore> logger)
    {
        _pathLayout = pathLayout;
        _logger = logger;
    }

    public async Task<ErrorRecord> AddAsync(ErrorRecord record, CancellationToken cancellationToken = default)
    {
        var bag = _records.GetOrAdd(record.Fecha, _ => new ConcurrentBag<ErrorRecord>());
        bag.Add(record);

        await PersistRecordAsync(record, cancellationToken).ConfigureAwait(false);
        return record;
    }

    public Task<IReadOnlyList<ErrorRecord>> GetRecordsForDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        if (!_records.TryGetValue(date, out var bag))
        {
            return Task.FromResult<IReadOnlyList<ErrorRecord>>(Array.Empty<ErrorRecord>());
        }

        var ordered = bag.OrderBy(r => r.Id).ThenBy(r => r.Hora).ToList();
        return Task.FromResult<IReadOnlyList<ErrorRecord>>(ordered);
    }

    public async Task LoadPersistedRecordsAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var registryPath = _pathLayout.GetRegistryFilePath(date);
        if (!File.Exists(registryPath))
        {
            return;
        }

        var lines = await File.ReadAllLinesAsync(registryPath, cancellationToken).ConfigureAwait(false);
        var bag = _records.GetOrAdd(date, _ => new ConcurrentBag<ErrorRecord>());
        var maxId = 0;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var record = JsonSerializer.Deserialize<ErrorRecord>(line);
                if (record is null)
                {
                    continue;
                }

                bag.Add(record);
                maxId = Math.Max(maxId, record.Id);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Linea invalida en registro de errores: {Line}", line);
            }
        }

        _counters.AddOrUpdate(date, maxId, (_, current) => Math.Max(current, maxId));
    }

    public int GetNextId(DateOnly date)
    {
        return _counters.AddOrUpdate(date, 1, (_, current) => current + 1);
    }

    private async Task PersistRecordAsync(ErrorRecord record, CancellationToken cancellationToken)
    {
        var registryPath = _pathLayout.GetRegistryFilePath(record.Fecha);
        var directory = Path.GetDirectoryName(registryPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(record);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(registryPath, json + Environment.NewLine, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
