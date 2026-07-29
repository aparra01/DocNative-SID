using DocNative.Core.Abstractions;
using DocNative.Core.Configuration;
using DocNative.Core.Models;
using DocNative.Sucursales.Watching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocNative.Sucursales.Services;

public sealed class DocumentProcessorService
{
    private readonly DocNativeOptions _options;
    private readonly IPathLayout _pathLayout;
    private readonly IDocumentPipeline _pipeline;
    private readonly IErrorHandler _errorHandler;
    private readonly FileStabilityChecker _stabilityChecker;
    private readonly SucursalResolver _sucursalResolver;
    private readonly ILogger<DocumentProcessorService> _logger;
    private readonly HashSet<string> _inProgress = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public DocumentProcessorService(
        IOptions<DocNativeOptions> options,
        IPathLayout pathLayout,
        IDocumentPipeline pipeline,
        IErrorHandler errorHandler,
        FileStabilityChecker stabilityChecker,
        SucursalResolver sucursalResolver,
        ILogger<DocumentProcessorService> logger)
    {
        _options = options.Value;
        _pathLayout = pathLayout;
        _pipeline = pipeline;
        _errorHandler = errorHandler;
        _stabilityChecker = stabilityChecker;
        _sucursalResolver = sucursalResolver;
        _logger = logger;
    }

    public async Task ProcessAsync(string pdfPath, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (!_inProgress.Add(pdfPath))
            {
                return;
            }
        }

        try
        {
            if (!File.Exists(pdfPath))
            {
                return;
            }

            if (pdfPath.EndsWith(".processing", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!_sucursalResolver.TryResolve(pdfPath, out var agencia))
            {
                await _errorHandler.HandleAsync(pdfPath, _options.SinSucursalCode, "Ruta fuera de ENTRADA root", cancellationToken).ConfigureAwait(false);
                return;
            }

            if (!await _stabilityChecker.WaitUntilStableAsync(pdfPath, cancellationToken).ConfigureAwait(false))
            {
                await _errorHandler.HandleAsync(pdfPath, agencia, "Archivo bloqueado o inestable", cancellationToken).ConfigureAwait(false);
                return;
            }

            var outputPath = pdfPath;
            var tempOutputPath = pdfPath + ".processing";

            PipelineResult result;
            try
            {
                result = _pipeline.Process(pdfPath, tempOutputPath);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(pdfPath, agencia, $"Error de lectura: {ex.Message}", cancellationToken).ConfigureAwait(false);
                return;
            }

            if (!result.Success || string.IsNullOrWhiteSpace(result.OutputPath))
            {
                await _errorHandler.HandleAsync(pdfPath, agencia, result.ErrorMessage ?? "Error de procesamiento", cancellationToken).ConfigureAwait(false);
                if (File.Exists(tempOutputPath))
                {
                    File.Delete(tempOutputPath);
                }

                return;
            }

            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            File.Move(result.OutputPath, outputPath, overwrite: true);

            _logger.LogInformation(
                "PDF pre-procesado in-place. Agencia={Agencia}, Archivo={Archivo}, Eliminadas={Removed}, Rotadas={Rotated}",
                agencia,
                Path.GetFileName(outputPath),
                result.PagesRemoved,
                result.PagesRotated);
        }
        finally
        {
            lock (_lock)
            {
                _inProgress.Remove(pdfPath);
            }
        }
    }
}
