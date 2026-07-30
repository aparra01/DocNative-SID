using DocNative.Core.Configuration;
using DocNative.Core.Pdf;
using Microsoft.Extensions.Options;
using OpenCvSharp;
using PdfSharpCore.Pdf.IO;

namespace DocNative.Core.Tests;

public class PagarePruebaDeepDiagnosticTests
{
    private const string DefaultPdfPath =
        @"C:\Users\ander\Desktop\PAGARES FORMATO ACTUAL\pagare_prueba hoja en blanco y volteao.pdf";

    [Fact]
    public void PagarePrueba_PdfMetadataAndPixelSamples()
    {
        var pdfPath = Environment.GetEnvironmentVariable("DOCNATIVE_TEST_PDF") ?? DefaultPdfPath;
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException($"PDF de prueba no encontrado: {pdfPath}");
        }

        using (var doc = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import))
        {
            for (var i = 0; i < doc.PageCount; i++)
            {
                var page = doc.Pages[i];
                Console.WriteLine(
                    $"PDF pagina {i + 1}: Rotate={page.Rotate} | Width={page.Width} Height={page.Height}");
            }
        }

        var options = Options.Create(new DocNativeOptions { RenderDpi = 150 });
        using var renderer = new PdfRenderService(options);
        var pages = renderer.RenderPages(pdfPath, 150);

        for (var i = 0; i < pages.Count; i++)
        {
            using var image = pages[i];
            using var gray = new Mat();
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGRA2GRAY);
            Cv2.MinMaxLoc(gray, out double minGray, out double maxGray);
            Cv2.MeanStdDev(gray, out var mean, out var stdDev);

            var sample = image.At<Vec4b>(image.Height / 2, image.Width / 2);
            Console.WriteLine(
                $"Render pagina {i + 1}: minGray={minGray} maxGray={maxGray} meanGray={mean.Val0:F1} stdGray={stdDev.Val0:F1} centerBGRA=({sample.Item0},{sample.Item1},{sample.Item2},{sample.Item3})");
        }

        foreach (var page in pages)
        {
            page.Dispose();
        }
    }
}
