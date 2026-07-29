using DocNative.Core.Imaging;
using DocNative.Core.Tests.Helpers;

namespace DocNative.Core.Tests;

public class RotationCorrectorTests
{
    private readonly RotationCorrector _corrector = new();

    [Fact]
    public void DetectPortraitCorrectionDegrees_ReturnsZero_ForAlreadyPortrait()
    {
        using var portrait = TestImageFactory.CreateWithContent(800, 1100);

        var correction = _corrector.DetectPortraitCorrectionDegrees(portrait);

        Assert.Equal(0, correction);
    }

    [Fact]
    public void DetectPortraitCorrectionDegrees_Returns90_ForLandscapeImage()
    {
        using var landscape = TestImageFactory.CreateWithContent(1100, 800);

        var correction = _corrector.DetectPortraitCorrectionDegrees(landscape);

        Assert.Equal(90, correction);
    }

    [Fact]
    public void DetectPortraitCorrectionDegrees_Returns180_ForUpsideDownPortrait()
    {
        using var portrait = TestImageFactory.CreateWithContent(800, 1100);
        using var upsideDown = TestImageFactory.RotateQuarterTurns(portrait, 2);

        var correction = _corrector.DetectPortraitCorrectionDegrees(upsideDown);

        Assert.Equal(180, correction);
    }

    [Fact]
    public void DetectPortraitCorrectionDegrees_ReturnsQuarterTurn_ForRotatedLandscape()
    {
        using var portrait = TestImageFactory.CreateWithContent(800, 1100);
        using var rotated = TestImageFactory.RotateQuarterTurns(portrait, 1);

        var correction = _corrector.DetectPortraitCorrectionDegrees(rotated);

        Assert.True(correction is 90 or 270);
    }

    [Fact]
    public void DetectPortraitCorrectionDegrees_ReturnsZero_ForContinuationPageWithBottomSignatures()
    {
        using var continuationPage = TestImageFactory.CreateWithMiddleAndBottomContent(800, 1100);

        var correction = _corrector.DetectPortraitCorrectionDegrees(continuationPage);

        Assert.Equal(0, correction);
    }
}
