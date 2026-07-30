using OpenCvSharp;

namespace DocNative.Core.Imaging;

internal static class ImageRotator
{
    public static Mat Apply(Mat source, int degrees)
    {
        var normalized = NormalizeRotation(degrees);
        if (normalized == 0)
        {
            return source.Clone();
        }

        var rotated = new Mat();
        var flag = normalized switch
        {
            90 => RotateFlags.Rotate90Clockwise,
            180 => RotateFlags.Rotate180,
            270 => RotateFlags.Rotate90Counterclockwise,
            _ => throw new ArgumentOutOfRangeException(nameof(degrees), degrees, "Rotacion no soportada")
        };

        Cv2.Rotate(source, rotated, flag);
        return rotated;
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
