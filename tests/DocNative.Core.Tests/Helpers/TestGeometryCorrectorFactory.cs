using DocNative.Core.Configuration;
using DocNative.Core.Imaging;
using DocNative.Core.Pdf;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DocNative.Core.Tests.Helpers;

internal static class TestGeometryCorrectorFactory
{
    internal static PageGeometryCorrector Create(DocNativeOptions? options = null)
    {
        options ??= new DocNativeOptions
        {
            TesseractExecutablePath = "__missing_tesseract__"
        };

        var configuredOptions = Options.Create(options);
        return new PageGeometryCorrector(
            configuredOptions,
            new TesseractOsdDetector(configuredOptions),
            new HeuristicOrientationDetector(),
            new OpenCvSkewDetector(configuredOptions),
            NullLogger<PageGeometryCorrector>.Instance);
    }

    internal static DocNativeOptions CreateDefaultOptions()
    {
        return new DocNativeOptions
        {
            BlankPageThreshold = 0.02,
            BlankPageInkRatioThreshold = 0.015,
            RenderDpi = 150,
            TesseractExecutablePath = Environment.GetEnvironmentVariable("DOCNATIVE_TESSERACT_PATH")
                ?? @"C:\Program Files\Tesseract-OCR\tesseract.exe"
        };
    }

    internal static PdfRewriteService CreatePdfRewriter(DocNativeOptions? options = null)
    {
        return new PdfRewriteService(Options.Create(options ?? CreateDefaultOptions()));
    }
}
