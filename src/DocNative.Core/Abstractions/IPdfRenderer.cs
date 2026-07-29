using OpenCvSharp;

namespace DocNative.Core.Abstractions;

public interface IPdfRenderer
{
    IReadOnlyList<Mat> RenderPages(string pdfPath, int dpi);
}
