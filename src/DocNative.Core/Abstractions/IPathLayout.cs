namespace DocNative.Core.Abstractions;

public interface IPathLayout
{
    string Normalize(string path);

    string GetAgencyOutputDirectory(string agencia);

    string GetAgencyErrorDirectory(DateOnly date, string agencia);

    string GetDateErrorDirectory(DateOnly date);

    string GetRegistryFilePath(DateOnly date);

    string GetCsvFilePath(DateOnly date);

    bool TryResolveAgencyFromEntradaPath(string pdfPath, out string agencia);

    bool TryResolveAgencyFromRawPath(string pdfPath, out string agencia);

    bool TryResolveAgencyFromStagingPath(string pdfPath, out string agencia);

    string GetProcesandoPath(string agencia, string fileName);

    string GetPreProcesadoPath(string agencia, string fileName);

    bool TryLocateRelocatedPdf(string fileName, int maxAgeMinutes, out string locatedPath);
}
