using System.Globalization;
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

    public string GetWorkRoot()
    {
        if (!string.IsNullOrWhiteSpace(_options.WorkRoot))
        {
            return Normalize(_options.WorkRoot);
        }

#pragma warning disable CS0618
        if (!string.IsNullOrWhiteSpace(_options.ProcesandoRoot))
        {
            return Normalize(_options.ProcesandoRoot);
        }
#pragma warning restore CS0618

        return Normalize(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DocNative",
            "work"));
    }

    public string GetAgencyOutputDirectory(string agencia) =>
        Path.Combine(Normalize(_options.OutputRoot), SanitizeAgency(agencia));

    public string GetAgencyErrorDirectory(DateOnly date, string agencia) =>
        GetDateErrorDirectory(date);

    public string GetDateErrorDirectory(DateOnly date) =>
        Path.Combine(EffectiveErrorRoot, FormatDateFolder(date));

    /// <summary>Ruta del CSV diario (layout central o legado según configuración).</summary>
    public string GetCsvFilePath(DateOnly date)
    {
        var dateFolder = FormatDateFolder(date);
        if (UsesCentralErrorLayout)
        {
            return Path.Combine(EffectiveErrorRoot, $"{dateFolder}_error.csv");
        }

        return Path.Combine(EffectiveErrorRoot, dateFolder, $"errores_{dateFolder}.csv");
    }

    public string GetProcesandoPath(string agencia, string fileName) =>
        Path.Combine(GetWorkRoot(), SanitizeAgency(agencia), fileName);

    public string GetListoPath(string agencia, string fileName) =>
        Path.Combine(GetAgencyOutputDirectory(agencia), DocNativeOptions.ListoSubfolderName, fileName);

    public string GetPreProcesadoPath(string agencia, string fileName) => GetListoPath(agencia, fileName);

    public bool IsListoDeliveryPath(string pdfPath)
    {
        var directory = Normalize(Path.GetDirectoryName(pdfPath) ?? string.Empty);
        var listoSegment = $"{Path.DirectorySeparatorChar}{DocNativeOptions.ListoSubfolderName}";
        var listoSegmentAlt = $"{Path.AltDirectorySeparatorChar}{DocNativeOptions.ListoSubfolderName}";
        return directory.EndsWith(listoSegment, StringComparison.OrdinalIgnoreCase)
            || directory.EndsWith(listoSegmentAlt, StringComparison.OrdinalIgnoreCase)
            || directory.Contains(
                $"{listoSegment}{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase)
            || directory.Contains(
                $"{listoSegmentAlt}{Path.AltDirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase);
    }

    public bool IsIntakeEntradaPath(string pdfPath)
    {
        var fullPdfPath = Normalize(pdfPath);
        var entradaRoot = Normalize(_options.OutputRoot);
        if (!fullPdfPath.StartsWith(entradaRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !IsListoDeliveryPath(pdfPath);
    }

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

        return TryResolveAgencyFromRoot(pdfPath, GetWorkRoot(), out agencia);
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

        var entradaRoot = Normalize(_options.OutputRoot);
        var salidaRoot = Normalize(_options.SalidaRoot);
        var errorRoot = EffectiveErrorRoot;

        if (Directory.Exists(entradaRoot))
        {
            foreach (var agencyDir in Directory.EnumerateDirectories(entradaRoot))
            {
                if (string.Equals(
                        Path.GetFileName(agencyDir),
                        DocNativeOptions.ListoSubfolderName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var listoDir = Path.Combine(agencyDir, DocNativeOptions.ListoSubfolderName);
                if (!Directory.Exists(listoDir))
                {
                    continue;
                }

                foreach (var path in Directory.EnumerateFiles(listoDir, fileName, SearchOption.TopDirectoryOnly))
                {
                    ConsiderMatch(path);
                }
            }
        }

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

        var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var firstSegment = segments[0];
        if (string.Equals(firstSegment, DocNativeOptions.ListoSubfolderName, StringComparison.OrdinalIgnoreCase)
            && segments.Length > 1)
        {
            firstSegment = segments[1];
        }

        if (string.IsNullOrWhiteSpace(firstSegment))
        {
            agencia = _options.SinSucursalCode;
            return true;
        }

        agencia = firstSegment.Trim().ToUpperInvariant();
        return true;
    }

    private bool UsesCentralErrorLayout =>
        _options.UsePagareOcrCentralErrorLayout || _options.EnableInterleavedPdfValidation;

    private string EffectiveErrorRoot
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_options.ErrorRoot))
            {
                return Normalize(_options.ErrorRoot);
            }

            if (UsesCentralErrorLayout)
            {
                return Path.Combine(Normalize(_options.SalidaRoot), DocNativeOptions.CentralErrorDirName);
            }

            return Path.Combine(Normalize(_options.SalidaRoot), "ERROR");
        }
    }

    private string FormatDateFolder(DateOnly date)
    {
        if (UsesCentralErrorLayout)
        {
            return date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        return date.ToString("dd_MM_yyyy", CultureInfo.InvariantCulture);
    }

    private static string SanitizeAgency(string agencia)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(agencia.Trim().Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "SIN_SUCURSAL" : cleaned.ToUpperInvariant();
    }
}
