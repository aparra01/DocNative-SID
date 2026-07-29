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

        using var gray = new Mat();
        if (image.Channels() == 1)
        {
            image.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        }

        Cv2.MeanStdDev(gray, out var mean, out var stdDev);
        var normalizedStdDev = stdDev.Val0 / 255.0;
        var normalizedMean = mean.Val0 / 255.0;

        return normalizedStdDev <= _options.BlankPageThreshold && normalizedMean >= 0.85;
    }
}
