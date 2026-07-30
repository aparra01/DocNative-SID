using DocNative.Core.Configuration;
using DocNative.Core.Imaging;
using DocNative.Core.Pdf;
using DocNative.Core.Pipeline;
using DocNative.Core.Paths;
using DocNative.Core.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PdfSharpCore.Pdf.IO;

namespace DocNative.Core.Tests;

[Collection(nameof(DocnetCollection))]
public class PagarePruebaIntegrationTests
{
    private const string DefaultPdfPath =
        @"C:\Users\ander\Desktop\PAGARES FORMATO ACTUAL\pagare_prueba_hoja en blanco y volteado version 2.pdf";

    [Fact]
    public void PagarePrueba_FullPipeline_RemovesBlankPageAndBakesRotatedPages()
    {
        var pdfPath = Environment.GetEnvironmentVariable("DOCNATIVE_TEST_PDF") ?? DefaultPdfPath;
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException($"PDF de prueba no encontrado: {pdfPath}");
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "docnative-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var outputPath = Path.Combine(tempDir, "output.pdf");

        try
        {
            var options = Options.Create(TestGeometryCorrectorFactory.CreateDefaultOptions());
            options.Value.OutputRoot = tempDir;

            using var renderer = new PdfRenderService(options);
            var pipeline = new DocumentPipeline(
                options,
                new PathLayout(options),
                renderer,
                new BlankPageDetector(options),
                TestGeometryCorrectorFactory.Create(options.Value),
                TestGeometryCorrectorFactory.CreatePdfRewriter(options.Value),
                NullLogger<DocumentPipeline>.Instance);

            var result = pipeline.Process(pdfPath, outputPath);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(1, result.PagesRemoved);
            Assert.Equal(2, result.PagesRotated);
            Assert.True(File.Exists(outputPath));

            using var output = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
            Assert.Equal(5, output.PageCount);
            Assert.All(Enumerable.Range(0, output.PageCount), i => Assert.Equal(0, output.Pages[i].Rotate));

            Console.WriteLine(
                $"Pipeline OK | eliminadas={result.PagesRemoved} rotadas={result.PagesRotated} | paginasSalida={output.PageCount}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
