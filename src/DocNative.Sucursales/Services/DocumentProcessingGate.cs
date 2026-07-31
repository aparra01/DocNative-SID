using DocNative.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocNative.Sucursales.Services;

/// <summary>
/// Limita cuántos PDFs procesa DocNative en paralelo (cola implícita vía SemaphoreSlim).
/// </summary>
public sealed class DocumentProcessingGate : IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger<DocumentProcessingGate> _logger;
    private readonly int _maxConcurrent;

    public DocumentProcessingGate(
        IOptions<DocNativeOptions> options,
        ILogger<DocumentProcessingGate> logger)
    {
        _logger = logger;
        _maxConcurrent = Math.Max(1, options.Value.MaxConcurrentDocuments);
        _semaphore = new SemaphoreSlim(_maxConcurrent, _maxConcurrent);
    }

    public int MaxConcurrent => _maxConcurrent;

    public int AvailableSlots => _semaphore.CurrentCount;

    public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        if (_semaphore.CurrentCount == 0)
        {
            _logger.LogInformation(
                "Cola DocNative: esperando slot de procesamiento | MaxConcurrent={MaxConcurrent}",
                _maxConcurrent);
        }

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "Cola DocNative: slot adquirido | Disponibles={Disponibles}/{MaxConcurrent}",
            _semaphore.CurrentCount,
            _maxConcurrent);

        return new Releaser(_semaphore);
    }

    public void Dispose() => _semaphore.Dispose();

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                semaphore.Release();
            }
        }
    }
}
