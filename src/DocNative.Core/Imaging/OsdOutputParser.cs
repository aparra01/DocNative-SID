using System.Globalization;
using System.Text.RegularExpressions;

namespace DocNative.Core.Imaging;

internal static partial class OsdOutputParser
{
    [GeneratedRegex(@"^\s*Rotate\s*:\s*(-?\d+(?:\.\d+)?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex RotateLineRegex();

    [GeneratedRegex(@"^\s*Orientation confidence\s*:\s*(-?\d+(?:\.\d+)?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex OrientationConfidenceRegex();

    internal static bool TryParse(string output, out int rotateDegrees, out float orientationConfidence)
    {
        rotateDegrees = 0;
        orientationConfidence = 0;

        if (string.IsNullOrWhiteSpace(output))
        {
            return false;
        }

        var rotateMatch = RotateLineRegex().Match(output);
        if (!rotateMatch.Success)
        {
            return false;
        }

        if (!double.TryParse(rotateMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var rotateValue))
        {
            return false;
        }

        rotateDegrees = NormalizeRotation((int)Math.Round(rotateValue));
        if (rotateDegrees is not (0 or 90 or 180 or 270))
        {
            return false;
        }

        var confidenceMatch = OrientationConfidenceRegex().Match(output);
        if (confidenceMatch.Success
            && float.TryParse(confidenceMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var confidence))
        {
            orientationConfidence = confidence;
        }

        return true;
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
}
