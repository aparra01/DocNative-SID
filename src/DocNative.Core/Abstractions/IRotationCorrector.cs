using DocNative.Core.Imaging;
using OpenCvSharp;

namespace DocNative.Core.Abstractions;

public interface IRotationCorrector
{
    PageGeometryCorrection DetectCorrection(Mat image);
}
