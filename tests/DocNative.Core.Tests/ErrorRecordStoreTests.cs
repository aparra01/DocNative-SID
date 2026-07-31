using DocNative.Core.Configuration;
using DocNative.Core.Errors;
using DocNative.Core.Models;
using DocNative.Core.Paths;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DocNative.Core.Tests;

public class ErrorRecordStoreTests
{
    [Fact]
    public async Task AppendErrorAsync_WritesHeaderAndFirstRow()
    {
        var outputDirectory = CreateTempDirectory();
        var date = new DateOnly(2026, 7, 28);
        var store = CreateStore(outputDirectory);

        try
        {
            await store.AppendErrorAsync(new ErrorRecord
            {
                Fecha = date,
                Hora = new TimeOnly(14, 32, 5),
                Agencia = "QUITO",
                NombrePdf = "lote.pdf",
                TipoError = "Orientacion indetectable"
            });

            var csvPath = Path.Combine(outputDirectory, "ERROR", "28_07_2026", "errores_28_07_2026.csv");
            var content = await File.ReadAllTextAsync(csvPath);

            Assert.Contains("#,Fecha,Hora,Agencia,Nombre PDF,Tipo Error", content);
            Assert.Contains("1,2026-07-28,14:32:05,QUITO,lote.pdf,Orientacion indetectable", content);
        }
        finally
        {
            CleanupDirectory(outputDirectory);
        }
    }

    [Fact]
    public async Task AppendErrorAsync_IncrementsRowIndexOnSecondAppend()
    {
        var outputDirectory = CreateTempDirectory();
        var date = new DateOnly(2026, 7, 28);
        var store = CreateStore(outputDirectory);

        try
        {
            await store.AppendErrorAsync(new ErrorRecord
            {
                Fecha = date,
                Hora = new TimeOnly(14, 32, 5),
                Agencia = "QUITO",
                NombrePdf = "lote.pdf",
                TipoError = "Orientacion indetectable"
            });

            await store.AppendErrorAsync(new ErrorRecord
            {
                Fecha = date,
                Hora = new TimeOnly(15, 10, 0),
                Agencia = "GYE001",
                NombrePdf = "scan.pdf",
                TipoError = "PDF corrupto"
            });

            var csvPath = Path.Combine(outputDirectory, "ERROR", "28_07_2026", "errores_28_07_2026.csv");
            var content = await File.ReadAllTextAsync(csvPath);

            Assert.Contains("1,2026-07-28,14:32:05,QUITO,lote.pdf,Orientacion indetectable", content);
            Assert.Contains("2,2026-07-28,15:10:00,GYE001,scan.pdf,PDF corrupto", content);
        }
        finally
        {
            CleanupDirectory(outputDirectory);
        }
    }

    [Fact]
    public async Task AppendErrorAsync_ReusesSameCsvFileForSameDay()
    {
        var outputDirectory = CreateTempDirectory();
        var date = new DateOnly(2026, 7, 28);
        var store = CreateStore(outputDirectory);

        try
        {
            await store.AppendErrorAsync(new ErrorRecord
            {
                Fecha = date,
                Hora = new TimeOnly(9, 0, 0),
                Agencia = "QUITO",
                NombrePdf = "a.pdf",
                TipoError = "error_a"
            });

            await store.AppendErrorAsync(new ErrorRecord
            {
                Fecha = date,
                Hora = new TimeOnly(10, 0, 0),
                Agencia = "GYE001",
                NombrePdf = "b.pdf",
                TipoError = "error_b"
            });

            var errorDir = Path.Combine(outputDirectory, "ERROR", "28_07_2026");
            var csvFiles = Directory.GetFiles(errorDir, "*.csv");

            Assert.Single(csvFiles);
            Assert.EndsWith("errores_28_07_2026.csv", csvFiles[0], StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupDirectory(outputDirectory);
        }
    }

    private static ErrorRecordStore CreateStore(string salidaRoot)
    {
        var options = Options.Create(new DocNativeOptions
        {
            SalidaRoot = salidaRoot,
            ErrorRoot = string.Empty
        });

        return new ErrorRecordStore(new PathLayout(options), options, NullLogger<ErrorRecordStore>.Instance);
    }

    private static string CreateTempDirectory() =>
        Path.Combine(Path.GetTempPath(), "docnative-tests", Guid.NewGuid().ToString("N"));

    private static void CleanupDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
