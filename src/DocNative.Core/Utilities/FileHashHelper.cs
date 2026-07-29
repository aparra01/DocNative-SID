using System.Security.Cryptography;

namespace DocNative.Core.Utilities;

public static class FileHashHelper
{
    public static string ComputeSha256Hex(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
