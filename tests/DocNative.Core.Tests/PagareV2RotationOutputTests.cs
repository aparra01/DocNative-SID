using DocNative.Core.Configuration;
using DocNative.Core.Imaging;
using DocNative.Core.Pdf;
using DocNative.Core.Pipeline;
using DocNative.Core.Paths;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PdfSharpCore.Pdf.IO;

namespace DocNative.Core.Tests;

[Collection(nameof(DocnetCollection))]
public class PagareV2RotationOutputTests
{
    private const string SourcePdf =
        @"C:\Users\ander\Desktop\PAGARES FORMATO ACTUAL\pagare_prueba_hoja en blanco y volteado version 2.pdf";

    [Fact]
    public void PagareV2_OutputPdfRotationMetadata_IsNormalizedToZero()
    {
        if (!File.Exists(SourcePdf))
        {
            throw new FileNotFoundException(SourcePdf);
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "docnative-rot-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var outputPath = Path.Combine(tempDir, "output.pdf");

        try
        {
            var options = Options.Create(new DocNativeOptions { RenderDpi = 150, OutputRoot = tempDir });
            using var renderer = new PdfRenderService(options);
            var pipeline = new DocumentPipeline(
                options,
                new PathLayout(options),
                renderer,
                new BlankPageDetector(options),
                new RotationCorrector(),
                new PdfRewriteService(),
                NullLogger<DocumentPipeline>.Instance);

            pipeline.Process(SourcePdf, outputPath);

            using var output = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
            Assert.Equal(5, output.PageCount);
            for (var i = 0; i < output.PageCount; i++)
            {
                Console.WriteLine($"  output page {i + 1}: Rotate={output.Pages[i].Rotate}");
                Assert.Equal(0, output.Pages[i].Rotate);
            }
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
