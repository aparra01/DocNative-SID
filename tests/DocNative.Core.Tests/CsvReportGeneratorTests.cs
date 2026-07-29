using DocNative.Core.Errors;
using DocNative.Core.Models;

namespace DocNative.Core.Tests;

public class CsvReportGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_WritesExpectedHeadersAndRows()
    {
        var generator = new CsvReportGenerator();
        var date = new DateOnly(2026, 7, 28);
        var outputDirectory = Path.Combine(Path.GetTempPath(), "docnative-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var records = new List<ErrorRecord>
            {
                new()
                {
                    Id = 1,
                    Fecha = date,
                    Hora = new TimeOnly(14, 32, 5),
                    Agencia = "QUITO",
                    NombrePdf = "lote.pdf",
                    TipoError = "Orientacion indetectable"
                },
                new()
                {
                    Id = 2,
                    Fecha = date,
                    Hora = new TimeOnly(15, 10, 0),
                    Agencia = "GYE001",
                    NombrePdf = "scan.pdf",
                    TipoError = "PDF corrupto"
                }
            };

            var csvPath = await generator.GenerateAsync(date, records, outputDirectory);
            var content = await File.ReadAllTextAsync(csvPath);

            Assert.Contains("#,Fecha,Hora,Agencia,Nombre PDF,Tipo Error", content);
            Assert.Contains("1,2026-07-28,14:32:05,QUITO,lote.pdf,Orientacion indetectable", content);
            Assert.Contains("2,2026-07-28,15:10:00,GYE001,scan.pdf,PDF corrupto", content);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }
}
