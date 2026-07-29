using DocNative.Core.Models;

namespace DocNative.Core.Abstractions;

public interface IPdfRewriter
{
    void Rewrite(string sourcePdfPath, string destinationPdfPath, IReadOnlyList<PageAnalysisResult> pages);
}
