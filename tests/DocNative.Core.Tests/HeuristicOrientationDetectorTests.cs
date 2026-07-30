using DocNative.Core.Imaging;

namespace DocNative.Core.Tests;

public class HeuristicOrientationDetectorTests
{
    private readonly HeuristicOrientationDetector _detector = new();

    [Fact]
    public void DetectCoarseRotationDegrees_ReturnsZero_ForAlreadyPortrait()
    {
        using var portrait = Helpers.TestImageFactory.CreateWithContent(800, 1100);

        var correction = _detector.DetectCoarseRotationDegrees(portrait);

        Assert.Equal(0, correction);
    }

    [Fact]
    public void DetectCoarseRotationDegrees_Returns90_ForLandscapeImage()
    {
        using var landscape = Helpers.TestImageFactory.CreateWithContent(1100, 800);

        var correction = _detector.DetectCoarseRotationDegrees(landscape);

        Assert.Equal(90, correction);
    }

    [Fact]
    public void DetectCoarseRotationDegrees_Returns180_ForUpsideDownPortrait()
    {
        using var portrait = Helpers.TestImageFactory.CreateWithContent(800, 1100);
        using var upsideDown = Helpers.TestImageFactory.RotateQuarterTurns(portrait, 2);

        var correction = _detector.DetectCoarseRotationDegrees(upsideDown);

        Assert.Equal(180, correction);
    }

    [Fact]
    public void DetectCoarseRotationDegrees_ReturnsQuarterTurn_ForRotatedLandscape()
    {
        using var portrait = Helpers.TestImageFactory.CreateWithContent(800, 1100);
        using var rotated = Helpers.TestImageFactory.RotateQuarterTurns(portrait, 1);

        var correction = _detector.DetectCoarseRotationDegrees(rotated);

        Assert.True(correction is 90 or 270);
    }

    [Fact]
    public void DetectCoarseRotationDegrees_ReturnsZero_ForContinuationPageWithBottomSignatures()
    {
        using var continuationPage = Helpers.TestImageFactory.CreateWithMiddleAndBottomContent(800, 1100);

        var correction = _detector.DetectCoarseRotationDegrees(continuationPage);

        Assert.Equal(0, correction);
    }
}
