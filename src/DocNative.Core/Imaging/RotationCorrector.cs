using DocNative.Core.Abstractions;
using OpenCvSharp;

namespace DocNative.Core.Imaging;

public sealed class RotationCorrector : IRotationCorrector
{
    public int DetectPortraitCorrectionDegrees(Mat image)
    {
        if (image.Empty())
        {
            return 0;
        }

        using var gray = ToGray(image);

        if (image.Width > image.Height)
        {
            return DetectLandscapeCorrection(gray);
        }

        return DetectUpsideDownCorrection(gray) ? 180 : 0;
    }

    private static int DetectLandscapeCorrection(Mat gray)
    {
        using var binary = new Mat();
        Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

        var left = Cv2.CountNonZero(binary[new Rect(0, 0, binary.Width / 2, binary.Height)]);
        var right = Cv2.CountNonZero(binary[new Rect(binary.Width / 2, 0, binary.Width - (binary.Width / 2), binary.Height)]);

        return right > left * 1.15 ? 270 : 90;
    }

    private static bool DetectUpsideDownCorrection(Mat gray)
    {
        using var binary = new Mat();
        Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

        var topHeight = Math.Max(1, binary.Height / 3);
        var bottomStart = binary.Height - topHeight;

        var top = Cv2.CountNonZero(binary[new Rect(0, 0, binary.Width, topHeight)]);
        var bottom = Cv2.CountNonZero(binary[new Rect(0, bottomStart, binary.Width, topHeight)]);

        return bottom > top * 1.25;
    }

    private static Mat ToGray(Mat image)
    {
        var gray = new Mat();
        if (image.Channels() == 1)
        {
            image.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        }

        return gray;
    }
}
