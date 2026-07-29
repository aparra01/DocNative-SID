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
        var destinationPath = Path.Combine(destinationDirectory, fileName);

        if (File.Exists(destinationPath))
        {
            var stamp = now.ToString("HHmmss");
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            destinationPath = Path.Combine(destinationDirectory, $"{baseName}_{stamp}{extension}");
        }

        if (File.Exists(sourcePdfPath))
        {
            File.Move(sourcePdfPath, destinationPath, overwrite: false);
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

        _logger.LogWarning(
            "PDF movido a error. Agencia={Agencia}, Archivo={Archivo}, TipoError={TipoError}, Destino={Destino}",
            agencia,
            destinationFileName,
            tipoError,
            destinationPath);
    }
}
