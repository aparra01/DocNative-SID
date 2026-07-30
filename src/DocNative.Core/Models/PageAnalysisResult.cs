namespace DocNative.Core.Models;

public sealed class PageAnalysisResult
{
    public int PageIndex { get; init; }

    public bool IsBlank { get; init; }

    public int RotationDegrees { get; init; }

    public int SourceRotation { get; init; }

    public double SkewDegrees { get; init; }

    public float OsdConfidence { get; init; }

    public string DetectionMethod { get; init; } = string.Empty;
}
