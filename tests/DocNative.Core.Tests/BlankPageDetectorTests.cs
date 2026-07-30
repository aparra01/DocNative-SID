using DocNative.Core.Configuration;
using DocNative.Core.Imaging;
using DocNative.Core.Tests.Helpers;
using Microsoft.Extensions.Options;
using OpenCvSharp;

namespace DocNative.Core.Tests;

public class BlankPageDetectorTests
{
    [Fact]
    public void IsBlank_ReturnsTrue_ForWhitePage()
    {
        var detector = CreateDetector(0.02);
        using var image = TestImageFactory.CreateBlank();

        Assert.True(detector.IsBlank(image));
    }

    [Fact]
    public void IsBlank_ReturnsTrue_ForUniformEmptyRender()
    {
        var detector = CreateDetector(0.02);
        using var image = new Mat(1100, 800, MatType.CV_8UC4, Scalar.All(0));

        Assert.True(detector.IsBlank(image));
    }

    [Fact]
    public void IsBlank_ReturnsFalse_ForPageWithContent()
    {
        var detector = CreateDetector(0.02);
        using var image = TestImageFactory.CreateWithContent();

        Assert.False(detector.IsBlank(image));
    }

    [Fact]
    public void IsBlank_RespectsThreshold()
    {
        var strictDetector = CreateDetector(0.001);
        using var image = TestImageFactory.CreateBlank();
        Cv2.Rectangle(image, new Rect(100, 100, 300, 400), new Scalar(0, 0, 0), -1);

        Assert.False(strictDetector.IsBlank(image));
    }

    private static BlankPageDetector CreateDetector(double threshold)
    {
        var options = Options.Create(new DocNativeOptions { BlankPageThreshold = threshold });
        return new BlankPageDetector(options);
    }
}
