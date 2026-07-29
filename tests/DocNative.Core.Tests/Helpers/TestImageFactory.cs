using OpenCvSharp;

namespace DocNative.Core.Tests.Helpers;

internal static class TestImageFactory
{
    public static Mat CreateBlank(int width = 800, int height = 1100)
    {
        return new Mat(height, width, MatType.CV_8UC3, Scalar.All(255));
    }

    public static Mat CreateWithContent(int width = 800, int height = 1100)
    {
        var image = CreateBlank(width, height);
        Cv2.Rectangle(image, new Rect(120, 120, 500, 700), new Scalar(0, 0, 0), 3);
        Cv2.PutText(image, "PAGARE", new Point(250, 300), HersheyFonts.HersheySimplex, 2, new Scalar(0, 0, 0), 3);
        return image;
    }

    public static Mat RotateQuarterTurns(Mat source, int quarterTurns)
    {
        var turns = ((quarterTurns % 4) + 4) % 4;
        var rotated = new Mat();
        switch (turns)
        {
            case 1:
                Cv2.Rotate(source, rotated, RotateFlags.Rotate90Clockwise);
                break;
            case 2:
                Cv2.Rotate(source, rotated, RotateFlags.Rotate180);
                break;
            case 3:
                Cv2.Rotate(source, rotated, RotateFlags.Rotate90Counterclockwise);
                break;
            default:
                source.CopyTo(rotated);
                break;
        }

        return rotated;
    }
}
