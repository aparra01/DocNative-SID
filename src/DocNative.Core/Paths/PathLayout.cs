using DocNative.Core.Abstractions;
using DocNative.Core.Configuration;
using Microsoft.Extensions.Options;

namespace DocNative.Core.Paths;

public sealed class PathLayout : IPathLayout
{
    private readonly DocNativeOptions _options;

    public PathLayout(IOptions<DocNativeOptions> options)
    {
        _options = options.Value;
    }

    public string Normalize(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public string GetAgencyOutputDirectory(string agencia) =>
        Path.Combine(Normalize(_options.OutputRoot), SanitizeAgency(agencia));

    public string GetAgencyErrorDirectory(DateOnly date, string agencia) =>
        Path.Combine(GetDateErrorDirectory(date), SanitizeAgency(agencia));

    public string GetDateErrorDirectory(DateOnly date) =>
        Path.Combine(Normalize(_options.ErrorRoot), date.ToString("yyyyMMdd"));

    public string GetRegistryFilePath(DateOnly date) =>
        Path.Combine(GetDateErrorDirectory(date), "_registry.jsonl");

    public string GetCsvFilePath(DateOnly date) =>
        Path.Combine(GetDateErrorDirectory(date), $"errores_{date:yyyyMMdd}.csv");

    public bool TryResolveAgencyFromRawPath(string pdfPath, out string agencia)
    {
        agencia = string.Empty;
        var rawRoot = Normalize(_options.RawRoot);
        var fullPdfPath = Normalize(pdfPath);
        var pdfDirectory = Normalize(Path.GetDirectoryName(fullPdfPath) ?? string.Empty);

        if (!pdfDirectory.StartsWith(rawRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relative = pdfDirectory[rawRoot.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(relative))
        {
            agencia = _options.SinSucursalCode;
            return true;
        }

        var firstSegment = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        if (string.IsNullOrWhiteSpace(firstSegment))
        {
            agencia = _options.SinSucursalCode;
            return true;
        }

        agencia = firstSegment.Trim().ToUpperInvariant();
        return true;
    }

    private static string SanitizeAgency(string agencia)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(agencia.Trim().Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "SIN_SUCURSAL" : cleaned.ToUpperInvariant();
    }
}
