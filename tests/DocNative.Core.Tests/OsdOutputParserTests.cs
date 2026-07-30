using DocNative.Core.Imaging;

namespace DocNative.Core.Tests;

public class OsdOutputParserTests
{
    [Fact]
    public void TryParse_ReturnsTrue_ForTypicalTesseractOsdOutput()
    {
        const string output = """
            Page number: 0
            Orientation in degrees: 270
            Rotate: 90
            Orientation confidence: 12.34
            Script: Latin
            Script confidence: 8.56
            """;

        var parsed = OsdOutputParser.TryParse(output, out var rotateDegrees, out var orientationConfidence);

        Assert.True(parsed);
        Assert.Equal(90, rotateDegrees);
        Assert.Equal(12.34f, orientationConfidence, precision: 2);
    }

    [Fact]
    public void TryParse_ReturnsFalse_WhenRotateLineMissing()
    {
        const string output = """
            Page number: 0
            Orientation confidence: 10.00
            """;

        var parsed = OsdOutputParser.TryParse(output, out _, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void TryParse_NormalizesNegativeRotateValues()
    {
        const string output = """
            Rotate: -90
            Orientation confidence: 4.20
            """;

        var parsed = OsdOutputParser.TryParse(output, out var rotateDegrees, out _);

        Assert.True(parsed);
        Assert.Equal(270, rotateDegrees);
    }
}
