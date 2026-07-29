using OpenCvSharp;

namespace DocNative.Core.Abstractions;

public interface IBlankPageDetector
{
    bool IsBlank(Mat image);
}
