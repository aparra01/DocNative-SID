using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using DocNative.Core.Abstractions;
using DocNative.Core.Configuration;
using DocNative.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocNative.Core.Errors;

public sealed class ErrorRecordStore : IErrorRecordStore
{
    private const string LegacyCsvHeader = "#,Fecha,Hora,Agencia,Nombre PDF,Tipo Error";
    private const string CentralCsvHeader = "#,Fecha,Hora,Agencia,Nombre PDF,Descripción Error";

    private readonly ConcurrentDictionary<string, int> _rowCountCache = new();
    private readonly IPathLayout _pathLayout;
    private readonly DocNativeOptions _options;
    private readonly ILogger<ErrorRecordStore> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public ErrorRecordStore(
        IPathLayout pathLayout,
        IOptions<DocNativeOptions> options,
        ILogger<ErrorRecordStore> logger)
    {
        _pathLayout = pathLayout;
        _options = options.Value;
        _logger = logger;
    }

    private bool UsesCentralErrorLayout =>
        _options.UsePagareOcrCentralErrorLayout || _options.EnableInterleavedPdfValidation;

    private string CsvHeader =>
        UsesCentralErrorLayout ? CentralCsvHeader : LegacyCsvHeader;

    public async Task AppendErrorAsync(ErrorRecord record, CancellationToken cancellationToken = default)
    {
        var csvPath = _pathLayout.GetCsvFilePath(record.Fecha);
        var directory = Path.GetDirectoryName(csvPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            WarnIfExtraCsvFiles(directory, csvPath);
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = GetNextIndex(csvPath);
            var line = FormatRow(index, record);
            var writeHeader = !File.Exists(csvPath) || new FileInfo(csvPath).Length == 0;

            await using var stream = new FileStream(
                csvPath,
                FileMode.OpenOrCreate,
                FileAccess.Write,
                FileShare.None);
            stream.Seek(0, SeekOrigin.End);

            await using var writer = new StreamWriter(stream, Encoding.UTF8);
            if (writeHeader)
            {
                await writer.WriteLineAsync(CsvHeader.AsMemory(), cancellationToken).ConfigureAwait(false);
            }

            await writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }

        _logger.LogInformation(
            "Error registrado en CSV. Agencia={Agencia}, Archivo={Archivo}, TipoError={TipoError}, Csv={CsvPath}",
            record.Agencia,
            record.NombrePdf,
            record.TipoError,
            csvPath);
    }

    private int GetNextIndex(string csvPath)
    {
        if (_rowCountCache.TryGetValue(csvPath, out var cached))
        {
            var next = cached + 1;
            _rowCountCache[csvPath] = next;
            return next;
        }

        var count = 0;
        if (File.Exists(csvPath))
        {
            try
            {
                count = Math.Max(0, File.ReadAllLines(csvPath).Length - 1);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "No se pudo leer CSV para indice: {CsvPath}", csvPath);
            }
        }

        var nextIndex = count + 1;
        _rowCountCache[csvPath] = nextIndex;
        return nextIndex;
    }

    private static string FormatRow(int index, ErrorRecord record)
    {
        var builder = new StringBuilder();
        builder.Append(index.ToString(CultureInfo.InvariantCulture));
        builder.Append(',');
        builder.Append(Escape(record.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        builder.Append(',');
        builder.Append(Escape(record.Hora.ToString("HH:mm:ss", CultureInfo.InvariantCulture)));
        builder.Append(',');
        builder.Append(Escape(record.Agencia));
        builder.Append(',');
        builder.Append(Escape(record.NombrePdf));
        builder.Append(',');
        builder.Append(Escape(record.TipoError));
        return builder.ToString();
    }

    private static string Escape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }

    private void WarnIfExtraCsvFiles(string errorDirectory, string canonicalCsvPath)
    {
        if (!Directory.Exists(errorDirectory))
        {
            return;
        }

        var canonicalName = Path.GetFileName(canonicalCsvPath);
        var extras = Directory.EnumerateFiles(errorDirectory, "*.csv")
            .Select(Path.GetFileName)
            .Where(name => !string.Equals(name, canonicalName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (extras.Count == 0)
        {
            return;
        }

        _logger.LogWarning(
            "Se espera un solo CSV por día en {ErrorDirectory}. Canonico={CanonicalCsv}. Extras={ExtraCsvFiles}",
            errorDirectory,
            canonicalName,
            string.Join(", ", extras));
    }
}
