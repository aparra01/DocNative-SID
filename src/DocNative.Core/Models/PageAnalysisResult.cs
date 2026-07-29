namespace DocNative.Core.Models;

public sealed class PageAnalysisResult
{
    public int PageIndex { get; init; }

    public bool IsBlank { get; init; }

    public int RotationDegrees { get; init; }
}
