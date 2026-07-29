using System.Globalization;
using System.Text;
using DocNative.Core.Abstractions;
using DocNative.Core.Models;

namespace DocNative.Core.Errors;

public sealed class CsvReportGenerator : ICsvReportGenerator
{
    public async Task<string> GenerateAsync(
        DateOnly date,
        IReadOnlyList<ErrorRecord> records,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);

        var csvPath = Path.Combine(outputDirectory, $"errores_{date:dd_MM_yyyy}.csv");
        var builder = new StringBuilder();
        builder.AppendLine("#,Fecha,Hora,Agencia,Nombre PDF,Tipo Error");

        var index = 1;
        foreach (var record in records.OrderBy(r => r.Id).ThenBy(r => r.Hora))
        {
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
            builder.AppendLine(Escape(record.TipoError));
            index++;
        }

        await File.WriteAllTextAsync(csvPath, builder.ToString(), Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        return csvPath;
    }

    private static string Escape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }
}
