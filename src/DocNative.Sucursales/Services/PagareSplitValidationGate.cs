using System.Text.Json;
using DocNative.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocNative.Sucursales.Services;

/// <summary>
/// Serializa (o limita) las llamadas HTTP a PagareSplit para evitar saturación y timeouts.
/// </summary>
public sealed class PagareSplitValidationGate : IDisposable
{
    private static readonly string DebugLogPath = ResolveDebugLogPath();

    private static string ResolveDebugLogPath()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10 && !string.IsNullOrEmpty(dir); i++)
        {
            var candidate = Path.Combine(dir, "debug-9bf231.log");
            if (Directory.Exists(Path.Combine(dir, "DocNative-SID")))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        return Path.Combine(AppContext.BaseDirectory, "debug-9bf231.log");
    }

    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger<PagareSplitValidationGate> _logger;
    private readonly int _maxConcurrent;

    public PagareSplitValidationGate(
        IOptions<DocNativeOptions> options,
        ILogger<PagareSplitValidationGate> logger)
    {
        _logger = logger;
        _maxConcurrent = Math.Max(1, options.Value.PagareSplitMaxConcurrentValidations);
        _semaphore = new SemaphoreSlim(_maxConcurrent, _maxConcurrent);
    }

    public int MaxConcurrent => _maxConcurrent;

    public async Task<IDisposable> AcquireAsync(string pdfPath, CancellationToken cancellationToken)
    {
        var waitStarted = DateTimeOffset.UtcNow;

        if (_semaphore.CurrentCount == 0)
        {
            _logger.LogInformation(
                "Cola PagareSplit: esperando slot de validación | Archivo={Archivo} | MaxConcurrent={MaxConcurrent}",
                pdfPath,
                _maxConcurrent);

            // #region agent log
            WriteDebugLog("H1", "PagareSplitValidationGate.cs:AcquireAsync", "waiting_for_slot", new
            {
                archivo = Path.GetFileName(pdfPath),
                maxConcurrent = _maxConcurrent,
            });
            // #endregion
        }

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        var waitMs = (DateTimeOffset.UtcNow - waitStarted).TotalMilliseconds;

        _logger.LogDebug(
            "Cola PagareSplit: slot adquirido | Archivo={Archivo} | EsperaMs={EsperaMs} | Disponibles={Disponibles}/{MaxConcurrent}",
            pdfPath,
            (int)waitMs,
            _semaphore.CurrentCount,
            _maxConcurrent);

        // #region agent log
        WriteDebugLog("H1", "PagareSplitValidationGate.cs:AcquireAsync", "slot_acquired", new
        {
            archivo = Path.GetFileName(pdfPath),
            waitMs = (int)waitMs,
            disponibles = _semaphore.CurrentCount,
            maxConcurrent = _maxConcurrent,
        });
        // #endregion

        return new Releaser(_semaphore);
    }

    public void Dispose() => _semaphore.Dispose();

    private static void WriteDebugLog(string hypothesisId, string location, string message, object data)
    {
        try
        {
            var payload = new
            {
                sessionId = "9bf231",
                runId = "queue-control",
                hypothesisId,
                location,
                message,
                data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            File.AppendAllText(DebugLogPath, JsonSerializer.Serialize(payload) + Environment.NewLine);
        }
        catch
        {
            // debug-only
        }
    }

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
