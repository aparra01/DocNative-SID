namespace DocNative.Core.Imaging;

public sealed record PageGeometryCorrection(
    int CoarseRotationDegrees,
    double SkewDegrees,
    float OsdConfidence,
    string DetectionMethod);
