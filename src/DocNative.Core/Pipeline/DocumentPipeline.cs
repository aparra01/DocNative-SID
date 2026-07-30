using DocNative.Core.Abstractions;
using DocNative.Core.Configuration;
using DocNative.Core.Imaging;
using DocNative.Core.Models;
using DocNative.Core.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PdfSharpCore.Pdf.IO;

namespace DocNative.Core.Pipeline;

public sealed class DocumentPipeline : IDocumentPipeline
{
    private const int RelocatedLookupMaxAgeMinutes = 30;

    private readonly DocNativeOptions _options;
    private readonly IPathLayout _pathLayout;
    private readonly IPdfRenderer _pdfRenderer;
    private readonly IBlankPageDetector _blankPageDetector;
    private readonly IRotationCorrector _rotationCorrector;
    private readonly IPdfRewriter _pdfRewriter;
    private readonly ILogger<DocumentPipeline> _logger;

    public DocumentPipeline(
        IOptions<DocNativeOptions> options,
        IPathLayout pathLayout,
        IPdfRenderer pdfRenderer,
        IBlankPageDetector blankPageDetector,
        IRotationCorrector rotationCorrector,
        IPdfRewriter pdfRewriter,
        ILogger<DocumentPipeline> logger)
    {
        _options = options.Value;
        _pathLayout = pathLayout;
        _pdfRenderer = pdfRenderer;
        _blankPageDetector = blankPageDetector;
        _rotationCorrector = rotationCorrector;
        _pdfRewriter = pdfRewriter;
        _logger = logger;
    }

    public PipelineResult Process(string sourcePdfPath, string destinationPdfPath)
    {
        if (!File.Exists(sourcePdfPath))
        {
            return TryRelocatedOrFail(sourcePdfPath, "Archivo no encontrado");
        }

        try
        {
            var sourceRotations = ReadSourcePageRotations(sourcePdfPath);
            var pageImages = _pdfRenderer.RenderPages(sourcePdfPath, _options.RenderDpi);
            var analysis = new List<PageAnalysisResult>(pageImages.Count);

            try
            {
                for (var i = 0; i < pageImages.Count; i++)
                {
                    var image = pageImages[i];
                    var blankMetrics = BlankPageDetector.Analyze(image, _options);
                    var isBlank = blankMetrics.IsBlank;
                    PageGeometryCorrection geometry;
                    if (isBlank)
                    {
                        geometry = new PageGeometryCorrection(0, 0, 0, "blank");
                    }
                    else
                    {
                        geometry = _rotationCorrector.DetectCorrection(image);
                    }

                    var sourceRotation = i < sourceRotations.Count ? sourceRotations[i] : 0;
                    var contentRotation = ResolveContentRotation(geometry.CoarseRotationDegrees, sourceRotation);
                    var bake = ShouldBakePage(contentRotation, sourceRotation, geometry.SkewDegrees);

                    _logger.LogInformation(
                        "Pagina {PageNumber}/{PageCount} | mean={Mean:F4} stdDev={StdDev:F4} ink={InkRatio:P2} uniformEmpty={UniformEmpty} | blank={IsBlank} | method={DetectionMethod} osdConf={OsdConfidence:F1} skew={SkewDegrees:F2} | pixelRot={PixelRotation} pdfRot={SourceRotation} contentRot={ContentRotation} bake={Bake}",
                        i + 1,
                        pageImages.Count,
                        blankMetrics.NormalizedMean,
                        blankMetrics.NormalizedStdDev,
                        blankMetrics.InkRatio,
                        blankMetrics.IsUniformEmptyRender,
                        isBlank,
                        geometry.DetectionMethod,
                        geometry.OsdConfidence,
                        geometry.SkewDegrees,
                        geometry.CoarseRotationDegrees,
                        sourceRotation,
                        contentRotation,
                        bake);

                    analysis.Add(new PageAnalysisResult
                    {
                        PageIndex = i,
                        IsBlank = isBlank,
                        RotationDegrees = contentRotation,
                        SourceRotation = sourceRotation,
                        SkewDegrees = geometry.SkewDegrees,
                        OsdConfidence = geometry.OsdConfidence,
                        DetectionMethod = geometry.DetectionMethod
                    });
                }

                if (analysis.All(p => p.IsBlank))
                {
                    return PipelineResult.Fail("Documento sin contenido util");
                }

                if (!File.Exists(sourcePdfPath))
                {
                    return TryRelocatedOrFail(sourcePdfPath, "Archivo no encontrado antes de reescritura");
                }

                _pdfRewriter.Rewrite(
                    sourcePdfPath,
                    destinationPdfPath,
                    analysis,
                    pageImages,
                    _options.RenderDpi);
            }
            finally
            {
                foreach (var image in pageImages)
                {
                    image.Dispose();
                }
            }

            var removed = analysis.Count(p => p.IsBlank);
            var rotated = analysis.Count(p => !p.IsBlank && IsPageCorrected(p));

            _logger.LogInformation(
                "PDF procesado {Source} -> {Destination}. Paginas eliminadas: {Removed}, rotadas: {Rotated}",
                sourcePdfPath,
                destinationPdfPath,
                removed,
                rotated);

            return PipelineResult.Ok(destinationPdfPath, removed, rotated);
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogDebug(ex, "PDF desaparecido durante procesamiento {Source}", sourcePdfPath);
            return TryRelocatedOrFail(sourcePdfPath, ex.Message);
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogDebug(ex, "PDF desaparecido durante procesamiento {Source}", sourcePdfPath);
            return TryRelocatedOrFail(sourcePdfPath, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return PipelineResult.Fail(ex.Message);
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "IO durante procesamiento PDF {Source}", sourcePdfPath);
            return TryRelocatedOrFail(sourcePdfPath, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando PDF {Source}", sourcePdfPath);
            return PipelineResult.Fail($"Error de procesamiento: {ex.Message}");
        }
    }

    internal static int ResolveContentRotation(int pixelRotation, int sourceRotation)
    {
        if (pixelRotation != 0)
        {
            return NormalizeRotation(pixelRotation);
        }

        return NormalizeRotation(sourceRotation);
    }

    private bool ShouldBakePage(int contentRotation, int sourceRotation, double skewDegrees)
    {
        return contentRotation != 0
            || sourceRotation != 0
            || Math.Abs(skewDegrees) >= _options.MinSkewDegrees;
    }

    private bool IsPageCorrected(PageAnalysisResult page)
    {
        return page.RotationDegrees != 0 || Math.Abs(page.SkewDegrees) >= _options.MinSkewDegrees;
    }

    private static IReadOnlyList<int> ReadSourcePageRotations(string sourcePdfPath)
    {
        using var input = PdfReader.Open(sourcePdfPath, PdfDocumentOpenMode.Import);
        var rotations = new List<int>(input.PageCount);
        for (var i = 0; i < input.PageCount; i++)
        {
            rotations.Add(NormalizeRotation(input.Pages[i].Rotate));
        }

        return rotations;
    }

    private static int NormalizeRotation(int degrees)
    {
        var normalized = degrees % 360;
        if (normalized < 0)
        {
            normalized += 360;
        }

        return normalized switch
        {
            0 or 90 or 180 or 270 => normalized,
            _ => 0
        };
    }

    private PipelineResult TryRelocatedOrFail(string sourcePdfPath, string fallbackMessage)
    {
        var fileName = Path.GetFileName(sourcePdfPath);
        if (_pathLayout.TryLocateRelocatedPdf(fileName, RelocatedLookupMaxAgeMinutes, out var locatedPath))
        {
            _logger.LogInformation(
                "PDF ya movido por otro proceso. Archivo={Archivo}, Ubicacion={Ubicacion}",
                fileName,
                locatedPath);
            return PipelineResult.Relocated(locatedPath);
        }

        return PipelineResult.Fail(fallbackMessage);
    }
}
