using DocNative.Core.Abstractions;
using DocNative.Core.Configuration;
using Microsoft.Extensions.Options;
using OpenCvSharp;

namespace DocNative.Core.Imaging;

public sealed class BlankPageDetector : IBlankPageDetector
{
    private readonly DocNativeOptions _options;

    public BlankPageDetector(IOptions<DocNativeOptions> options)
    {
        _options = options.Value;
    }

    public bool IsBlank(Mat image)
    {
        if (image.Empty())
        {
            return true;
        }

        if (IsUniformEmptyRender(image))
        {
            return true;
        }

        using var gray = ToGray(image);
        Cv2.MeanStdDev(gray, out var mean, out var stdDev);
        var normalizedStdDev = stdDev.Val0 / 255.0;
        var normalizedMean = mean.Val0 / 255.0;

        if (normalizedStdDev <= _options.BlankPageThreshold && normalizedMean >= 0.85)
        {
            return true;
        }

        var inkRatio = ComputeInkRatio(gray);
        return inkRatio <= _options.BlankPageInkRatioThreshold;
    }

    internal static BlankPageMetrics Analyze(Mat image, DocNativeOptions options)
    {
        if (image.Empty())
        {
            return new BlankPageMetrics(true, 0, 0, 0, true);
        }

        var uniformEmpty = IsUniformEmptyRender(image);
        using var gray = ToGray(image);
        Cv2.MeanStdDev(gray, out var mean, out var stdDev);
        var normalizedMean = mean.Val0 / 255.0;
        var normalizedStdDev = stdDev.Val0 / 255.0;
        var inkRatio = ComputeInkRatio(gray);

        var isBlank = uniformEmpty
            || (normalizedStdDev <= options.BlankPageThreshold && normalizedMean >= 0.85)
            || inkRatio <= options.BlankPageInkRatioThreshold;

        return new BlankPageMetrics(isBlank, normalizedMean, normalizedStdDev, inkRatio, uniformEmpty);
    }

    private static bool IsUniformEmptyRender(Mat image)
    {
        using var gray = ToGray(image);
        Cv2.MinMaxLoc(gray, out double _, out var maxGray);
        if (maxGray <= 1.0)
        {
            return true;
        }

        if (image.Channels() == 4)
        {
            using var alpha = new Mat();
            Cv2.ExtractChannel(image, alpha, 3);
            Cv2.MinMaxLoc(alpha, out double _, out var maxAlpha);
            if (maxAlpha <= 1.0)
            {
                return true;
            }
        }

        return false;
    }

    private static double ComputeInkRatio(Mat gray)
    {
        using var binary = new Mat();
        Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
        var totalPixels = binary.Width * binary.Height;
        if (totalPixels == 0)
        {
            return 0;
        }

        return (double)Cv2.CountNonZero(binary) / totalPixels;
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
            var conversion = image.Channels() == 4
                ? ColorConversionCodes.BGRA2GRAY
                : ColorConversionCodes.BGR2GRAY;
            Cv2.CvtColor(image, gray, conversion);
        }

        return gray;
    }
}

internal readonly record struct BlankPageMetrics(
    bool IsBlank,
    double NormalizedMean,
    double NormalizedStdDev,
    double InkRatio,
    bool IsUniformEmptyRender);
