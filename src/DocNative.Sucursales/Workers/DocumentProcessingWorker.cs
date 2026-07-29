using DocNative.Core.Abstractions;
using DocNative.Sucursales.Services;
using DocNative.Sucursales.Watching;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DocNative.Sucursales.Workers;

public sealed class DocumentProcessingWorker : BackgroundService
{
    private readonly HotfolderWatcher _hotfolderWatcher;
    private readonly DocumentProcessorService _processor;
    private readonly IErrorRecordStore _errorRecordStore;
    private readonly ILogger<DocumentProcessingWorker> _logger;
    private CancellationToken _stoppingToken;

    public DocumentProcessingWorker(
        HotfolderWatcher hotfolderWatcher,
        DocumentProcessorService processor,
        IErrorRecordStore errorRecordStore,
        ILogger<DocumentProcessingWorker> logger)
    {
        _hotfolderWatcher = hotfolderWatcher;
        _processor = processor;
        _errorRecordStore = errorRecordStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
        await _errorRecordStore.LoadPersistedRecordsAsync(DateOnly.FromDateTime(DateTime.Now), stoppingToken).ConfigureAwait(false);

        _hotfolderWatcher.PdfDetected += OnPdfDetected;
        _hotfolderWatcher.Start();

        _logger.LogInformation("DocumentProcessingWorker iniciado");

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // apagado normal del host
        }
        finally
        {
            _hotfolderWatcher.PdfDetected -= OnPdfDetected;
            await _hotfolderWatcher.StopAsync(CancellationToken.None).ConfigureAwait(false);
            _logger.LogInformation("DocumentProcessingWorker detenido");
        }
    }

    private Task OnPdfDetected(string path) =>
        _processor.ProcessAsync(path, _stoppingToken);
}
