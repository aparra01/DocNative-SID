using DocNative.Core.Configuration;
using DocNative.Core.Imaging;
using DocNative.Core.Tests.Helpers;

namespace DocNative.Core.Tests;

public class PageGeometryCorrectorTests
{
    [Fact]
    public void DetectCorrection_UsesHeuristicFallback_WhenTesseractMissing()
    {
        var corrector = TestGeometryCorrectorFactory.Create(new DocNativeOptions
        {
            TesseractExecutablePath = "__missing_tesseract__",
            EnableDeskew = false
        });

        using var portrait = TestImageFactory.CreateWithContent(800, 1100);
        using var upsideDown = TestImageFactory.RotateQuarterTurns(portrait, 2);

        var correction = corrector.DetectCorrection(upsideDown);

        Assert.Equal("heuristic", correction.DetectionMethod);
        Assert.Equal(180, correction.CoarseRotationDegrees);
    }

    [Fact]
    public void DetectCorrection_ReturnsEmptyGeometry_ForEmptyImage()
    {
        var corrector = TestGeometryCorrectorFactory.Create();

        using var empty = new OpenCvSharp.Mat();
        var correction = corrector.DetectCorrection(empty);

        Assert.Equal("empty", correction.DetectionMethod);
        Assert.Equal(0, correction.CoarseRotationDegrees);
        Assert.Equal(0, correction.SkewDegrees);
    }
}
