using DocNative.Core.Abstractions;
using DocNative.Core.Models;
using Microsoft.Extensions.Logging;

namespace DocNative.Core.Errors;

public sealed class ErrorHandler : IErrorHandler
{
    private readonly IPathLayout _pathLayout;
    private readonly IErrorRecordStore _errorRecordStore;
    private readonly ILogger<ErrorHandler> _logger;

    public ErrorHandler(
        IPathLayout pathLayout,
        IErrorRecordStore errorRecordStore,
        ILogger<ErrorHandler> logger)
    {
        _pathLayout = pathLayout;
        _errorRecordStore = errorRecordStore;
        _logger = logger;
    }

    public async Task HandleAsync(string sourcePdfPath, string agencia, string tipoError, CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var date = DateOnly.FromDateTime(now);
        var destinationDirectory = _pathLayout.GetDateErrorDirectory(date);
        Directory.CreateDirectory(destinationDirectory);

        var fileName = Path.GetFileName(sourcePdfPath);
        var canonicalDestination = Path.Combine(destinationDirectory, fileName);

        if (!File.Exists(sourcePdfPath) && File.Exists(canonicalDestination))
        {
            _logger.LogInformation(
                "Error ya registrado, omitiendo duplicado. Agencia={Agencia}, Archivo={Archivo}, TipoError={TipoError}, Destino={Destino}",
                agencia,
                fileName,
                tipoError,
                canonicalDestination);
            return;
        }

        var destinationPath = canonicalDestination;

        if (File.Exists(destinationPath))
        {
            var stamp = now.ToString("HHmmss");
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            destinationPath = Path.Combine(destinationDirectory, $"{baseName}_{stamp}{extension}");
        }

        var moved = false;
        if (File.Exists(sourcePdfPath))
        {
            File.Move(sourcePdfPath, destinationPath, overwrite: false);
            moved = true;
        }

        var destinationFileName = Path.GetFileName(destinationPath);
        var record = new ErrorRecord
        {
            Fecha = date,
            Hora = TimeOnly.FromDateTime(now),
            Agencia = agencia,
            NombrePdf = destinationFileName,
            TipoError = tipoError,
            SourcePath = sourcePdfPath,
            DestinationPath = destinationPath
        };

        await _errorRecordStore.AppendErrorAsync(record, cancellationToken).ConfigureAwait(false);

        if (moved)
        {
            _logger.LogWarning(
                "PDF movido a error. Agencia={Agencia}, Archivo={Archivo}, TipoError={TipoError}, Destino={Destino}",
                agencia,
                destinationFileName,
                tipoError,
                destinationPath);
        }
        else
        {
            _logger.LogWarning(
                "Error registrado sin archivo fuente. Agencia={Agencia}, Archivo={Archivo}, TipoError={TipoError}, Fuente={Fuente}",
                agencia,
                destinationFileName,
                tipoError,
                sourcePdfPath);
        }
    }
}
