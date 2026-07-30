using OpenCvSharp;

namespace DocNative.Core.Imaging;

internal static class ImageRotator
{
    public static Mat Apply(Mat source, int coarseDegrees, double skewDegrees, double minSkewDegrees)
    {
        using var coarse = ApplyCoarse(source, coarseDegrees);
        if (Math.Abs(skewDegrees) < minSkewDegrees)
        {
            return coarse.Clone();
        }

        return ApplySkew(coarse, skewDegrees);
    }

    internal static Mat ApplyCoarse(Mat source, int degrees)
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

    private static Mat ApplySkew(Mat source, double skewDegrees)
    {
        var center = new Point2f(source.Width / 2f, source.Height / 2f);
        using var matrix = Cv2.GetRotationMatrix2D(center, skewDegrees, 1.0);

        var cos = Math.Abs(matrix.At<double>(0, 0));
        var sin = Math.Abs(matrix.At<double>(0, 1));
        var newWidth = (int)Math.Ceiling(source.Height * sin + source.Width * cos);
        var newHeight = (int)Math.Ceiling(source.Height * cos + source.Width * sin);

        matrix.Set(0, 2, matrix.At<double>(0, 2) + (newWidth / 2.0) - center.X);
        matrix.Set(1, 2, matrix.At<double>(1, 2) + (newHeight / 2.0) - center.Y);

        var background = source.Channels() == 4
            ? new Scalar(255, 255, 255, 255)
            : new Scalar(255, 255, 255);

        var result = new Mat();
        Cv2.WarpAffine(
            source,
            result,
            matrix,
            new Size(newWidth, newHeight),
            InterpolationFlags.Linear,
            BorderTypes.Constant,
            background);
        return result;
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
