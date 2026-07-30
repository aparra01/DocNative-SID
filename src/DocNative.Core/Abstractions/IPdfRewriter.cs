using DocNative.Core.Models;
using OpenCvSharp;

namespace DocNative.Core.Abstractions;

public interface IPdfRewriter
{
    void Rewrite(
        string sourcePdfPath,
        string destinationPdfPath,
        IReadOnlyList<PageAnalysisResult> pages,
        IReadOnlyList<Mat> pageImages,
        int renderDpi);
}
