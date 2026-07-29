using DocNative.Core.Abstractions;
using DocNative.Core.Models;
using Microsoft.Extensions.Logging;

namespace DocNative.Core.Errors;

public sealed class ErrorHandler : IErrorHandler
{
    private readonly IPathLayout _pathLayout;
    private readonly ErrorRecordStore _errorRecordStore;
    private readonly ILogger<ErrorHandler> _logger;

    public ErrorHandler(
        IPathLayout pathLayout,
        ErrorRecordStore errorRecordStore,
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
        var destinationDirectory = _pathLayout.GetAgencyErrorDirectory(date, agencia);
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

        var record = new ErrorRecord
        {
            Id = _errorRecordStore.GetNextId(date),
            Fecha = date,
            Hora = TimeOnly.FromDateTime(now),
            Agencia = agencia,
            NombrePdf = fileName,
            TipoError = tipoError,
            SourcePath = sourcePdfPath,
            DestinationPath = destinationPath
        };

        await _errorRecordStore.AddAsync(record, cancellationToken).ConfigureAwait(false);

        _logger.LogWarning(
            "PDF movido a error. Agencia={Agencia}, Archivo={Archivo}, TipoError={TipoError}, Destino={Destino}",
            agencia,
            fileName,
            tipoError,
            destinationPath);
    }
}
