using DocNative.Core.Configuration;
using DocNative.Core.Errors;
using DocNative.Core.Models;
using DocNative.Core.Paths;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DocNative.Core.Tests;

public class ErrorHandlerTests
{
    [Fact]
    public async Task HandleAsync_SkipsDuplicateCsvWhenSourceMissingAndCanonicalDestinationExists()
    {
        var salidaRoot = CreateTempDirectory();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var dateFolder = today.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var errorDirectory = Path.Combine(salidaRoot, "00 - ERROR", dateFolder);
        Directory.CreateDirectory(errorDirectory);

        var pdfName = "ARCHIVO DESORDENADO.pdf";
        var canonicalPath = Path.Combine(errorDirectory, pdfName);
        await File.WriteAllTextAsync(canonicalPath, "pdf");

        var handler = CreateHandler(salidaRoot);
        var workPath = Path.Combine(Path.GetTempPath(), "docnative-tests", Guid.NewGuid().ToString("N"), pdfName);

        await handler.HandleAsync(
            workPath,
            "CARAPUNGO",
            "PDF mal ordenado: operaciones intercaladas");

        var csvPath = Path.Combine(salidaRoot, "00 - ERROR", $"{dateFolder}_error.csv");
        Assert.False(File.Exists(csvPath));
        Assert.True(File.Exists(canonicalPath));
        Assert.False(Directory.EnumerateFiles(errorDirectory, "*.pdf").Skip(1).Any());

        CleanupDirectory(salidaRoot);
    }

    [Fact]
    public async Task HandleAsync_SecondCallAfterFirstMove_DoesNotDuplicateCsv()
    {
        var salidaRoot = CreateTempDirectory();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var dateFolder = today.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var workDirectory = Path.Combine(Path.GetTempPath(), "docnative-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectory);

        var pdfName = "ARCHIVO DESORDENADO.pdf";
        var sourcePath = Path.Combine(workDirectory, pdfName);
        await File.WriteAllTextAsync(sourcePath, "pdf");

        var handler = CreateHandler(salidaRoot);

        await handler.HandleAsync(sourcePath, "CARAPUNGO", "PDF mal ordenado: operaciones intercaladas");
        await handler.HandleAsync(sourcePath, "CARAPUNGO", "PDF mal ordenado: operaciones intercaladas");

        var csvPath = Path.Combine(salidaRoot, "00 - ERROR", $"{dateFolder}_error.csv");
        var content = await File.ReadAllTextAsync(csvPath);
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.DoesNotContain("_", lines[1].Split(',')[4], StringComparison.Ordinal);

        var errorDirectory = Path.Combine(salidaRoot, "00 - ERROR", dateFolder);
        Assert.Single(Directory.EnumerateFiles(errorDirectory, "*.pdf"));

        CleanupDirectory(salidaRoot);
        CleanupDirectory(workDirectory);
    }

    [Fact]
    public async Task HandleAsync_WritesSingleCsvRowWhenMovingSourceToError()
    {
        var salidaRoot = CreateTempDirectory();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var dateFolder = today.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var workDirectory = Path.Combine(Path.GetTempPath(), "docnative-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectory);

        var pdfName = "lote.pdf";
        var sourcePath = Path.Combine(workDirectory, pdfName);
        await File.WriteAllTextAsync(sourcePath, "pdf");

        var handler = CreateHandler(salidaRoot);

        await handler.HandleAsync(sourcePath, "CARAPUNGO", "PDF mal ordenado");

        var csvPath = Path.Combine(salidaRoot, "00 - ERROR", $"{dateFolder}_error.csv");
        var content = await File.ReadAllTextAsync(csvPath);
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.Contains("lote.pdf", lines[1], StringComparison.Ordinal);
        Assert.False(File.Exists(sourcePath));

        CleanupDirectory(salidaRoot);
        CleanupDirectory(workDirectory);
    }

    private static ErrorHandler CreateHandler(string salidaRoot)
    {
        var options = Options.Create(new DocNativeOptions
        {
            SalidaRoot = salidaRoot,
            ErrorRoot = string.Empty,
            UsePagareOcrCentralErrorLayout = true
        });

        var pathLayout = new PathLayout(options);
        var store = new ErrorRecordStore(pathLayout, options, NullLogger<ErrorRecordStore>.Instance);
        return new ErrorHandler(pathLayout, store, NullLogger<ErrorHandler>.Instance);
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
