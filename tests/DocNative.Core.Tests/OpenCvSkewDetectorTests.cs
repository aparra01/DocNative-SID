using DocNative.Core.Configuration;
using DocNative.Core.Imaging;
using DocNative.Core.Tests.Helpers;
using Microsoft.Extensions.Options;
using OpenCvSharp;

namespace DocNative.Core.Tests;

public class OpenCvSkewDetectorTests
{
    [Fact]
    public void DetectSkewDegrees_ReturnsNearZero_ForUprightContent()
    {
        var options = Options.Create(new DocNativeOptions
        {
            EnableDeskew = true,
            MinSkewDegrees = 0.3,
            MaxSkewDegrees = 15,
            BlankPageInkRatioThreshold = 0.015
        });
        var detector = new OpenCvSkewDetector(options);

        using var upright = TestImageFactory.CreateWithContent(800, 1100);
        var skew = detector.DetectSkewDegrees(upright);

        Assert.True(Math.Abs(skew) < 5);
    }

    [Fact]
    public void DetectSkewDegrees_ReturnsNonZero_ForTiltedSyntheticPage()
    {
        var options = Options.Create(new DocNativeOptions
        {
            EnableDeskew = true,
            MinSkewDegrees = 0.3,
            MaxSkewDegrees = 15,
            BlankPageInkRatioThreshold = 0.001
        });
        var detector = new OpenCvSkewDetector(options);

        using var upright = TestImageFactory.CreateWithContent(900, 1200);
        using var tilted = RotateSkew(upright, 4.0);
        var skew = detector.DetectSkewDegrees(tilted);

        Assert.InRange(Math.Abs(skew), 1.0, 8.0);
    }

    [Fact]
    public void DetectSkewDegrees_ReturnsZero_WhenDeskewDisabled()
    {
        var options = Options.Create(new DocNativeOptions { EnableDeskew = false });
        var detector = new OpenCvSkewDetector(options);

        using var upright = TestImageFactory.CreateWithContent(800, 1100);
        var skew = detector.DetectSkewDegrees(upright);

        Assert.Equal(0, skew);
    }

    private static Mat RotateSkew(Mat source, double degrees)
    {
        var center = new Point2f(source.Width / 2f, source.Height / 2f);
        using var matrix = Cv2.GetRotationMatrix2D(center, degrees, 1.0);
        var rotated = new Mat();
        Cv2.WarpAffine(
            source,
            rotated,
            matrix,
            source.Size(),
            InterpolationFlags.Linear,
            BorderTypes.Constant,
            Scalar.All(255));
        return rotated;
    }
}
