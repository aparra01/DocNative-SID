using OpenCvSharp;

namespace DocNative.Core.Imaging;

internal static class ImageGrayHelper
{
    internal static Mat ToGray(Mat image)
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
