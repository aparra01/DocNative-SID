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

        var thirdHeight = Math.Max(1, binary.Height / 3);

        var top = CountInk(binary, new Rect(0, 0, binary.Width, thirdHeight));
        var middle = CountInk(binary, new Rect(0, thirdHeight, binary.Width, thirdHeight));
        var bottom = CountInk(
            binary,
            new Rect(0, 2 * thirdHeight, binary.Width, binary.Height - (2 * thirdHeight)));

        var total = top + middle + bottom;
        if (total == 0)
        {
            return false;
        }

        // Hojas de continuación: cuerpo en el tercio central y firmas al pie (sin encabezado arriba).
        if (middle >= top && middle > bottom * 0.85)
        {
            return false;
        }

        const double TopThirdCenter = 1.0 / 6.0;
        const double MiddleThirdCenter = 3.0 / 6.0;
        const double BottomThirdCenter = 5.0 / 6.0;

        var centerOfMass = (top * TopThirdCenter + middle * MiddleThirdCenter + bottom * BottomThirdCenter) / total;
        var topShare = (double)top / total;

        // Solo rotar cuando casi todo el contenido quedó en la mitad inferior y arriba hay poco encabezado.
        return centerOfMass > 0.52 && topShare < 0.25;
    }

    private static int CountInk(Mat binary, Rect region)
    {
        return Cv2.CountNonZero(binary[region]);
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
