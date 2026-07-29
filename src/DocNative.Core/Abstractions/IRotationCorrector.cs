using OpenCvSharp;

namespace DocNative.Core.Abstractions;

public interface IRotationCorrector
{
    int DetectPortraitCorrectionDegrees(Mat image);
}
