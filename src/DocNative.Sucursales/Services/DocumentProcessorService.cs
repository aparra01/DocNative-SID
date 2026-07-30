using DocNative.Core.Abstractions;
using DocNative.Core.Configuration;
using DocNative.Core.Models;
using DocNative.Core.Utilities;
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
    private readonly SucursalesPdfOrderValidator _orderValidator;
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
        SucursalesPdfOrderValidator orderValidator,
        ILogger<DocumentProcessorService> logger)
    {
        _options = options.Value;
        _pathLayout = pathLayout;
        _pipeline = pipeline;
        _errorHandler = errorHandler;
        _stabilityChecker = stabilityChecker;
        _sucursalResolver = sucursalResolver;
        _orderValidator = orderValidator;
        _logger = logger;
    }

    public async Task ProcessAsync(string pdfPath, CancellationToken cancellationToken)
    {
        if (pdfPath.EndsWith(".processing", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (_pathLayout.IsListoDeliveryPath(pdfPath))
        {
            _logger.LogDebug("PDF en LISTO ignorado por DocNative | Ruta={Ruta}", pdfPath);
            return;
        }

        if (!_sucursalResolver.TryResolve(pdfPath, out var agencia))
        {
            if (File.Exists(pdfPath))
            {
                await _errorHandler.HandleAsync(pdfPath, _options.SinSucursalCode, "Ruta fuera de carpetas de staging", cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        var fileName = Path.GetFileName(pdfPath);
        var workingPath = _pathLayout.GetProcesandoPath(agencia, fileName);
        var listoPath = _pathLayout.GetListoPath(agencia, fileName);

        lock (_lock)
        {
            if (!_inProgress.Add(workingPath))
            {
                _logger.LogDebug(
                    "PDF ya en procesamiento | Agencia={Agencia} | Archivo={Archivo} | Clave={Clave}",
                    agencia,
                    fileName,
                    workingPath);
                return;
            }
        }

        try
        {
            if (File.Exists(listoPath))
            {
                _logger.LogInformation(
                    "PDF ya entregado en LISTO, omitiendo | Agencia={Agencia} | Archivo={Archivo} | Destino={Destino}",
                    agencia,
                    fileName,
                    listoPath);
                return;
            }

            if (!File.Exists(pdfPath))
            {
                if (File.Exists(workingPath))
                {
                    pdfPath = workingPath;
                }
                else if (_pathLayout.TryLocateRelocatedPdf(fileName, 30, out var locatedPath))
                {
                    _logger.LogInformation(
                        "PDF ya procesado por otro proceso | Agencia={Agencia} | Archivo={Archivo} | Ubicacion={Ubicacion}",
                        agencia,
                        fileName,
                        locatedPath);
                    return;
                }
                else
                {
                    _logger.LogDebug("PDF no encontrado al iniciar procesamiento | Ruta={Ruta}", pdfPath);
                    return;
                }
            }

            var normalizedPath = _pathLayout.Normalize(pdfPath);
            var workRoot = _pathLayout.GetWorkRoot();
            var alreadyInWork = normalizedPath.StartsWith(workRoot, StringComparison.OrdinalIgnoreCase);

            string correlationId;

            if (alreadyInWork)
            {
                try
                {
                    correlationId = FileHashHelper.ComputeSha256Hex(workingPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo calcular hash | Ruta={Ruta}", workingPath);
                    correlationId = "desconocido";
                }

                _logger.LogInformation(
                    "Reanudando PDF en WORK | CorrelationId={CorrelationId} | Ruta={Ruta}",
                    correlationId,
                    workingPath);
            }
            else
            {
                if (!await _stabilityChecker.WaitUntilStableAsync(pdfPath, cancellationToken).ConfigureAwait(false))
                {
                    await _errorHandler.HandleAsync(pdfPath, agencia, "Archivo bloqueado o inestable", cancellationToken).ConfigureAwait(false);
                    return;
                }

                try
                {
                    correlationId = FileHashHelper.ComputeSha256Hex(pdfPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo calcular hash | Ruta={Ruta}", pdfPath);
                    correlationId = "desconocido";
                }

                Directory.CreateDirectory(Path.GetDirectoryName(workingPath)!);

                try
                {
                    File.Move(pdfPath, workingPath);
                }
                catch (FileNotFoundException)
                {
                    _logger.LogInformation(
                        "Archivo ya reclamado | CorrelationId={CorrelationId} | Ruta={Ruta}",
                        correlationId,
                        pdfPath);
                    return;
                }
                catch (IOException ex)
                {
                    _logger.LogInformation(
                        ex,
                        "Claim fallido (otro worker?) | CorrelationId={CorrelationId} | Ruta={Ruta}",
                        correlationId,
                        pdfPath);
                    return;
                }

                _logger.LogInformation(
                    "PDF reclamado en WORK | CorrelationId={CorrelationId} | Agencia={Agencia} | Archivo={Archivo} | Destino={Destino}",
                    correlationId,
                    agencia,
                    fileName,
                    workingPath);
            }

            var outputFileName = Path.GetFileName(workingPath);

            var tempOutputPath = workingPath + ".processing";

            var (orderValid, orderError) = await _orderValidator.ValidateAsync(workingPath, cancellationToken).ConfigureAwait(false);
            if (!orderValid)
            {
                await _errorHandler.HandleAsync(
                    workingPath,
                    agencia,
                    orderError ?? "PDF mal ordenado",
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            PipelineResult result;
            try
            {
                result = _pipeline.Process(workingPath, tempOutputPath);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(workingPath, agencia, $"Error de lectura: {ex.Message}", cancellationToken).ConfigureAwait(false);
                return;
            }

            if (result.ErrorKind == PipelineErrorKind.RelocatedByOtherProcess)
            {
                _logger.LogInformation(
                    "Archivo ya procesado por otro proceso | CorrelationId={CorrelationId} | Ubicacion={Ubicacion}",
                    correlationId,
                    result.RelocatedPath);
                CleanupTempFile(tempOutputPath);
                return;
            }

            if (!result.Success || string.IsNullOrWhiteSpace(result.OutputPath))
            {
                await _errorHandler.HandleAsync(workingPath, agencia, result.ErrorMessage ?? "Error de procesamiento", cancellationToken).ConfigureAwait(false);
                CleanupTempFile(tempOutputPath);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(listoPath)!);

            if (File.Exists(listoPath))
            {
                File.Delete(listoPath);
            }

            File.Move(result.OutputPath, listoPath, overwrite: true);
            CleanupSourceFile(workingPath);

            _logger.LogInformation(
                "PDF entregado a LISTO | CorrelationId={CorrelationId} | Agencia={Agencia} | Archivo={Archivo} | Eliminadas={Removed} | Rotadas={Rotated} | Destino={Destino}",
                correlationId,
                agencia,
                outputFileName,
                result.PagesRemoved,
                result.PagesRotated,
                listoPath);
        }
        finally
        {
            lock (_lock)
            {
                _inProgress.Remove(workingPath);
            }
        }
    }

    private static void CleanupTempFile(string tempOutputPath)
    {
        if (File.Exists(tempOutputPath))
        {
            File.Delete(tempOutputPath);
        }
    }

    private static void CleanupSourceFile(string workingPath)
    {
        if (File.Exists(workingPath))
        {
            File.Delete(workingPath);
        }
    }
}
