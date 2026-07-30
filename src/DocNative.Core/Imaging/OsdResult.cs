namespace DocNative.Core.Imaging;

internal sealed record OsdResult(
    int RotateDegrees,
    float OrientationConfidence,
    bool Success,
    string? ErrorMessage,
    string? StandardError)
{
    internal static OsdResult Failed(string message, string? standardError = null)
    {
        return new OsdResult(0, 0, false, message, standardError);
    }
}
