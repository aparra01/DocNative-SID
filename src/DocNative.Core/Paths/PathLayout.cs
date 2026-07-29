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
        GetDateErrorDirectory(date);

    public string GetDateErrorDirectory(DateOnly date) =>
        Path.Combine(EffectiveErrorRoot, FormatDateFolder(date));

    public string GetRegistryFilePath(DateOnly date) =>
        Path.Combine(GetDateErrorDirectory(date), "_registry.jsonl");

    public string GetCsvFilePath(DateOnly date) =>
        Path.Combine(GetDateErrorDirectory(date), $"errores_{FormatDateFolder(date)}.csv");

    public string GetProcesandoPath(string agencia, string fileName) =>
        Path.Combine(Normalize(_options.ProcesandoRoot), SanitizeAgency(agencia), fileName);

    public string GetPreProcesadoPath(string agencia, string fileName) =>
        Path.Combine(Normalize(_options.PreProcesadoRoot), SanitizeAgency(agencia), fileName);

    public bool TryResolveAgencyFromEntradaPath(string pdfPath, out string agencia) =>
        TryResolveAgencyFromRoot(pdfPath, Normalize(_options.OutputRoot), out agencia);

    public bool TryResolveAgencyFromRawPath(string pdfPath, out string agencia) =>
        TryResolveAgencyFromEntradaPath(pdfPath, out agencia);

    public bool TryResolveAgencyFromStagingPath(string pdfPath, out string agencia)
    {
        if (TryResolveAgencyFromRoot(pdfPath, Normalize(_options.OutputRoot), out agencia))
        {
            return true;
        }

        if (TryResolveAgencyFromRoot(pdfPath, Normalize(_options.ProcesandoRoot), out agencia))
        {
            return true;
        }

        return TryResolveAgencyFromRoot(pdfPath, Normalize(_options.PreProcesadoRoot), out agencia);
    }

    public bool TryLocateRelocatedPdf(string fileName, int maxAgeMinutes, out string locatedPath)
    {
        locatedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var cutoff = DateTime.Now.AddMinutes(-maxAgeMinutes);
        string? bestPath = null;
        var bestTime = DateTime.MinValue;

        void ConsiderMatch(string path)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || !info.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (info.LastWriteTime > bestTime && info.LastWriteTime >= cutoff)
                {
                    bestTime = info.LastWriteTime;
                    bestPath = path;
                }
            }
            catch
            {
                // ignore inaccessible paths during scan
            }
        }

        var salidaRoot = Normalize(_options.SalidaRoot);
        var errorRoot = EffectiveErrorRoot;

        if (Directory.Exists(errorRoot))
        {
            foreach (var path in Directory.EnumerateFiles(errorRoot, fileName, SearchOption.AllDirectories))
            {
                ConsiderMatch(path);
            }
        }

        if (Directory.Exists(salidaRoot))
        {
            foreach (var agencyDir in Directory.EnumerateDirectories(salidaRoot))
            {
                var procesadosDir = Path.Combine(agencyDir, "PROCESADOS");
                if (!Directory.Exists(procesadosDir))
                {
                    continue;
                }

                foreach (var path in Directory.EnumerateFiles(procesadosDir, fileName, SearchOption.TopDirectoryOnly))
                {
                    ConsiderMatch(path);
                }
            }
        }

        if (bestPath is null)
        {
            return false;
        }

        locatedPath = bestPath;
        return true;
    }

    private bool TryResolveAgencyFromRoot(string pdfPath, string root, out string agencia)
    {
        agencia = string.Empty;
        var fullPdfPath = Normalize(pdfPath);
        var pdfDirectory = Normalize(Path.GetDirectoryName(fullPdfPath) ?? string.Empty);

        if (!pdfDirectory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relative = pdfDirectory[root.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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

    private string EffectiveErrorRoot
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_options.ErrorRoot))
            {
                return Normalize(_options.ErrorRoot);
            }

            return Path.Combine(Normalize(_options.SalidaRoot), "ERROR");
        }
    }

    private static string FormatDateFolder(DateOnly date) => date.ToString("dd_MM_yyyy");

    private static string SanitizeAgency(string agencia)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(agencia.Trim().Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "SIN_SUCURSAL" : cleaned.ToUpperInvariant();
    }
}
