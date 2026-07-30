using DocNative.Core.Configuration;
using DocNative.Core.Imaging;
using DocNative.Core.Pdf;
using DocNative.Core.Pipeline;
using DocNative.Core.Paths;
using DocNative.Core.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DocNative.Core.Tests;

[Collection(nameof(DocnetCollection))]
public class AmazonasPage3DiagnosticTests
{
    private const string TargetPdf =
        @"C:\Users\ander\Desktop\PAGARES FORMATO ACTUAL\20260728130115258.pdf";

    [Fact]
    public void AmazonasPdf_FullPipeline_RotatesAmbiguousPage3ByConsensus()
    {
        if (!File.Exists(TargetPdf))
        {
            throw new FileNotFoundException($"PDF no encontrado: {TargetPdf}");
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "docnative-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var outputPath = Path.Combine(tempDir, "output.pdf");

        try
        {
            var options = Options.Create(new DocNativeOptions
            {
                RenderDpi = 300,
                BlankPageThreshold = 0.02,
                BlankPageInkRatioThreshold = 0.015,
                OsdMinConfidence = 0.5,
                OsdMinConfidenceForRotation = 0.25,
                OsdMinConfidenceForUpright = 2.0,
                OsdMinCharactersToTry = 10,
                OsdMaxEdgePixels = 2000,
                EnableDocumentRotationConsensus = true,
                DocumentRotationConsensusMinShare = 0.6,
                TesseractExecutablePath = Environment.GetEnvironmentVariable("DOCNATIVE_TESSERACT_PATH")
                    ?? @"C:\Program Files\Tesseract-OCR\tesseract.exe"
            });

            using var renderer = new PdfRenderService(options);
            var pipeline = new DocumentPipeline(
                options,
                new PathLayout(options),
                renderer,
                new BlankPageDetector(options),
                TestGeometryCorrectorFactory.Create(options.Value),
                TestGeometryCorrectorFactory.CreatePdfRewriter(options.Value),
                NullLogger<DocumentPipeline>.Instance);

            var result = pipeline.Process(TargetPdf, outputPath);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(result.PagesRotated >= 10, $"Se esperaban >=10 rotadas, hubo {result.PagesRotated}");
            Assert.True(File.Exists(outputPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void AllPages_DiagnosticOsd()
    {
        if (!File.Exists(TargetPdf))
        {
            throw new FileNotFoundException($"PDF no encontrado: {TargetPdf}");
        }

        var options = Options.Create(new DocNativeOptions
        {
            RenderDpi = 150,
            TesseractExecutablePath = Environment.GetEnvironmentVariable("DOCNATIVE_TESSERACT_PATH")
                ?? @"C:\Program Files\Tesseract-OCR\tesseract.exe"
        });

        using var renderer = new PdfRenderService(options);
        var pages = renderer.RenderPages(TargetPdf, 150);
        var osd = new TesseractOsdDetector(options);
        var corrector = TestGeometryCorrectorFactory.Create(options.Value);

        for (var i = 0; i < pages.Count; i++)
        {
            using var page = pages[i];
            var osdResult = osd.Detect(page);
            var correction = corrector.DetectCorrection(page.Clone());
            Console.WriteLine(
                $"Pagina {i + 1}: osd rotate={osdResult.RotateDegrees} conf={osdResult.OrientationConfidence:F2} | corrector={correction.CoarseRotationDegrees} method={correction.DetectionMethod}");
        }

        foreach (var page in pages)
        {
            page.Dispose();
        }
    }

    [Fact]
    public void Page3_DiagnosticOsdFlipAndHeuristic()
    {
        if (!File.Exists(TargetPdf))
        {
            throw new FileNotFoundException($"PDF no encontrado: {TargetPdf}");
        }

        var options = Options.Create(new DocNativeOptions
        {
            RenderDpi = 150,
            OsdMinConfidence = 0.5,
            OsdMinConfidenceForRotation = 0.25,
            OsdMinConfidenceForUpright = 2.0,
            OsdMinCharactersToTry = 10,
            OsdMaxEdgePixels = 2000,
            TesseractExecutablePath = Environment.GetEnvironmentVariable("DOCNATIVE_TESSERACT_PATH")
                ?? @"C:\Program Files\Tesseract-OCR\tesseract.exe"
        });

        using var renderer = new PdfRenderService(options);
        var pages = renderer.RenderPages(TargetPdf, 150);
        Assert.True(pages.Count >= 3);

        using var page3 = pages[2];
        var osd = new TesseractOsdDetector(options);
        var heuristic = new HeuristicOrientationDetector();
        var corrector = TestGeometryCorrectorFactory.Create(options.Value);

        var originalOsd = osd.Detect(page3);
        using var flipped = ImageRotator.ApplyCoarse(page3, 180);
        var flippedOsd = osd.Detect(flipped);
        var heuristicRotation = heuristic.DetectCoarseRotationDegrees(page3);
        var correction = corrector.DetectCorrection(page3);

        Console.WriteLine($"Page3 size: {page3.Width}x{page3.Height}");
        Console.WriteLine(
            $"Original OSD: success={originalOsd.Success} rotate={originalOsd.RotateDegrees} conf={originalOsd.OrientationConfidence:F2} err={originalOsd.ErrorMessage}");
        Console.WriteLine(
            $"Flipped OSD:  success={flippedOsd.Success} rotate={flippedOsd.RotateDegrees} conf={flippedOsd.OrientationConfidence:F2} err={flippedOsd.ErrorMessage}");
        Console.WriteLine($"Heuristic: {heuristicRotation}°");
        Console.WriteLine(
            $"Corrector: method={correction.DetectionMethod} coarse={correction.CoarseRotationDegrees} conf={correction.OsdConfidence:F2}");

        foreach (var page in pages)
        {
            page.Dispose();
        }
    }
}
