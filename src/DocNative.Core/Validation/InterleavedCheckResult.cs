namespace DocNative.Core.Validation;

public sealed record InterleavedCheckResult(
    bool IsInterleaved,
    string? Message,
    IReadOnlyDictionary<string, int[]>? PagesByCode)
{
    public static InterleavedCheckResult Ok() => new(false, null, null);

    public static InterleavedCheckResult Fail(string message, IReadOnlyDictionary<string, int[]> pagesByCode) =>
        new(true, message, pagesByCode);
}
