using DocNative.Core.Configuration;
using Microsoft.Extensions.Options;
using OpenCvSharp;

namespace DocNative.Core.Imaging;

public sealed class OpenCvSkewDetector
{
    private readonly DocNativeOptions _options;

    public OpenCvSkewDetector(IOptions<DocNativeOptions> options)
    {
        _options = options.Value;
    }

    internal double DetectSkewDegrees(Mat image)
    {
        if (image.Empty() || !_options.EnableDeskew)
        {
            return 0;
        }

        using var gray = ImageGrayHelper.ToGray(image);
        using var binary = new Mat();
        Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

        var totalPixels = binary.Width * binary.Height;
        if (totalPixels == 0)
        {
            return 0;
        }

        var inkRatio = (double)Cv2.CountNonZero(binary) / totalPixels;
        if (inkRatio <= _options.BlankPageInkRatioThreshold)
        {
            return 0;
        }

        using var nonZero = new Mat();
        Cv2.FindNonZero(binary, nonZero);
        if (nonZero.Empty())
        {
            return 0;
        }

        try
        {
            var rotatedRect = Cv2.MinAreaRect(nonZero);
            var angle = rotatedRect.Angle;
            if (angle < -45)
            {
                angle += 90;
            }

            if (Math.Abs(angle) < _options.MinSkewDegrees)
            {
                return 0;
            }

            if (Math.Abs(angle) > _options.MaxSkewDegrees)
            {
                return Math.Sign(angle) * _options.MaxSkewDegrees;
            }

            return angle;
        }
        catch
        {
            return 0;
        }
    }
}
