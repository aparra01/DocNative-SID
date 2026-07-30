using DocNative.Core.Configuration;
using DocNative.Core.Imaging;
using DocNative.Core.Pdf;
using DocNative.Core.Pipeline;
using Microsoft.Extensions.Options;
using OpenCvSharp;

namespace DocNative.Core.Tests;

public class PagarePruebaPipelineTests
{
    private const string DefaultPdfPath =
        @"C:\Users\ander\Desktop\PAGARES FORMATO ACTUAL\pagare_prueba hoja en blanco y volteao.pdf";

    [Fact]
    public void PagarePrueba_DiagnosticPipeline_LogsPerPageMetrics()
    {
        var pdfPath = Environment.GetEnvironmentVariable("DOCNATIVE_TEST_PDF") ?? DefaultPdfPath;
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException($"PDF de prueba no encontrado: {pdfPath}");
        }

        var options = Options.Create(new DocNativeOptions
        {
            BlankPageThreshold = 0.02,
            BlankPageInkRatioThreshold = 0.015,
            RenderDpi = 150
        });
        using var renderer = new PdfRenderService(options);
        var blankDetector = new BlankPageDetector(options);
        var rotationCorrector = new RotationCorrector();

        var sourceRotations = ReadSourceRotations(pdfPath);
        var pages = renderer.RenderPages(pdfPath, 150);
        Assert.Equal(6, pages.Count);

        var blankPages = new List<int>();
        var rotatedPages = new List<int>();

        for (var i = 0; i < pages.Count; i++)
        {
            using var image = pages[i];
            var metrics = BlankPageDetector.Analyze(image, options.Value);
            var isBlank = blankDetector.IsBlank(image);
            var pixelRotation = isBlank ? 0 : rotationCorrector.DetectPortraitCorrectionDegrees(image);
            var sourceRotation = sourceRotations[i];
            var finalRotation = DocumentPipeline.ResolveRotationDegrees(pixelRotation, sourceRotation);

            Assert.Equal(metrics.IsBlank, isBlank);

            Console.WriteLine(
                $"Pagina {i + 1}/{pages.Count} | {image.Width}x{image.Height} | " +
                $"mean={metrics.NormalizedMean:F4} stdDev={metrics.NormalizedStdDev:F4} inkRatio={metrics.InkRatio:P2} uniformEmpty={metrics.IsUniformEmptyRender} | " +
                $"blank={isBlank} | pixelRot={pixelRotation} pdfRot={sourceRotation} finalRot={finalRotation}");

            if (isBlank)
            {
                blankPages.Add(i + 1);
            }

            if (!isBlank && finalRotation != 0)
            {
                rotatedPages.Add(i + 1);
            }
        }

        foreach (var page in pages)
        {
            page.Dispose();
        }

        Assert.Contains(2, blankPages);
        Assert.Contains(1, rotatedPages);
        Assert.DoesNotContain(3, blankPages);
    }

    [Fact]
    public void IsBlank_ReturnsTrue_ForUniformEmptyRenderLikePagarePage2()
    {
        var detector = CreateDetector();
        using var image = new Mat(1100, 800, MatType.CV_8UC4, Scalar.All(0));

        Assert.True(detector.IsBlank(image));
    }

    [Theory]
    [InlineData(0, 180, 180)]
    [InlineData(180, 180, 180)]
    [InlineData(90, 0, 90)]
    [InlineData(0, 0, 0)]
    public void ResolveRotationDegrees_PrefersPixelDetectionThenPdfMetadata(
        int pixelRotation,
        int sourceRotation,
        int expected)
    {
        var resolved = DocumentPipeline.ResolveRotationDegrees(pixelRotation, sourceRotation);
        Assert.Equal(expected, resolved);
    }

    private static BlankPageDetector CreateDetector()
    {
        var options = Options.Create(new DocNativeOptions
        {
            BlankPageThreshold = 0.02,
            BlankPageInkRatioThreshold = 0.015
        });
        return new BlankPageDetector(options);
    }

    private static IReadOnlyList<int> ReadSourceRotations(string pdfPath)
    {
        using var doc = PdfSharpCore.Pdf.IO.PdfReader.Open(pdfPath, PdfSharpCore.Pdf.IO.PdfDocumentOpenMode.Import);
        var rotations = new List<int>(doc.PageCount);
        for (var i = 0; i < doc.PageCount; i++)
        {
            rotations.Add(doc.Pages[i].Rotate);
        }

        return rotations;
    }
}
