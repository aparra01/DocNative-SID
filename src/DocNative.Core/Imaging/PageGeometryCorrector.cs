using DocNative.Core.Abstractions;
using DocNative.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenCvSharp;

namespace DocNative.Core.Imaging;

public sealed class PageGeometryCorrector : IRotationCorrector
{
    private readonly DocNativeOptions _options;
    private readonly TesseractOsdDetector _osdDetector;
    private readonly HeuristicOrientationDetector _heuristicDetector;
    private readonly OpenCvSkewDetector _skewDetector;
    private readonly ILogger<PageGeometryCorrector> _logger;

    public PageGeometryCorrector(
        IOptions<DocNativeOptions> options,
        TesseractOsdDetector osdDetector,
        HeuristicOrientationDetector heuristicDetector,
        OpenCvSkewDetector skewDetector,
        ILogger<PageGeometryCorrector> logger)
    {
        _options = options.Value;
        _osdDetector = osdDetector;
        _heuristicDetector = heuristicDetector;
        _skewDetector = skewDetector;
        _logger = logger;
    }

    public PageGeometryCorrection DetectCorrection(Mat image)
    {
        if (image.Empty())
        {
            return new PageGeometryCorrection(0, 0, 0, "empty");
        }

        var osdResult = _osdDetector.Detect(image);
        int coarseRotation;
        float osdConfidence;
        string detectionMethod;

        if (ShouldAcceptOsdResult(osdResult))
        {
            coarseRotation = osdResult.RotateDegrees;
            osdConfidence = osdResult.OrientationConfidence;
            detectionMethod = "osd";

            if (coarseRotation == 0
                && osdConfidence < _options.OsdMinConfidenceForUpright)
            {
                coarseRotation = TryFlipCheckCorrection(image, osdConfidence, ref detectionMethod);
            }
        }
        else
        {
            coarseRotation = _heuristicDetector.DetectCoarseRotationDegrees(image);
            osdConfidence = osdResult.OrientationConfidence;
            detectionMethod = "heuristic";

            if (!ShouldAcceptOsdResult(osdResult))
            {
                var reason = osdResult.Success
                    ? DescribeRejectedOsdConfidence(osdResult)
                    : osdResult.ErrorMessage ?? "OSD fallido";
                var stderr = Truncate(osdResult.StandardError, 300);
                _logger.LogWarning(
                    "Fallback heurístico de orientación. Motivo={Reason}. Stderr={Stderr}",
                    reason,
                    stderr);
            }
        }

        var skewDegrees = DetectSkewAfterCoarse(image, coarseRotation);

        return new PageGeometryCorrection(
            coarseRotation,
            skewDegrees,
            osdConfidence,
            detectionMethod);
    }

    private double DetectSkewAfterCoarse(Mat image, int coarseRotation)
    {
        if (!_options.EnableDeskew || coarseRotation == 0)
        {
            return _skewDetector.DetectSkewDegrees(image);
        }

        using var preview = ImageRotator.ApplyCoarse(image, coarseRotation);
        return _skewDetector.DetectSkewDegrees(preview);
    }

    private int TryFlipCheckCorrection(Mat image, float originalConfidence, ref string detectionMethod)
    {
        using var flipped = ImageRotator.ApplyCoarse(image, 180);
        var flippedOsd = _osdDetector.Detect(flipped);
        if (!flippedOsd.Success
            || flippedOsd.RotateDegrees != 0
            || flippedOsd.OrientationConfidence < _options.OsdMinConfidenceForUpright)
        {
            var heuristicRotation = _heuristicDetector.DetectCoarseRotationDegrees(image);
            if (heuristicRotation == 0)
            {
                return 0;
            }

            _logger.LogInformation(
                "Heurística corrige OSD 0° (conf={OrigConf:F1}) -> {Rotation}°",
                originalConfidence,
                heuristicRotation);
            detectionMethod = "osd+heuristic";
            return heuristicRotation;
        }

        _logger.LogInformation(
            "OSD flip-check: original 0° conf={OrigConf:F1}, invertida 0° conf={FlipConf:F1} -> 180°",
            originalConfidence,
            flippedOsd.OrientationConfidence);
        detectionMethod = "osd+flip";
        return 180;
    }

    private bool ShouldAcceptOsdResult(OsdResult osdResult)
    {
        if (!osdResult.Success)
        {
            return false;
        }

        if (osdResult.OrientationConfidence >= _options.OsdMinConfidence)
        {
            return true;
        }

        return osdResult.RotateDegrees != 0
            && osdResult.OrientationConfidence >= _options.OsdMinConfidenceForRotation;
    }

    private string DescribeRejectedOsdConfidence(OsdResult osdResult)
    {
        if (osdResult.RotateDegrees != 0)
        {
            return $"confianza OSD {osdResult.OrientationConfidence:F2} < {_options.OsdMinConfidenceForRotation:F2} (rotación {osdResult.RotateDegrees}°)";
        }

        return $"confianza OSD {osdResult.OrientationConfidence:F2} < {_options.OsdMinConfidence:F2}";
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
