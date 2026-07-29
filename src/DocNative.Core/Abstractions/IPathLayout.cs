namespace DocNative.Core.Abstractions;

public interface IPathLayout
{
    string Normalize(string path);

    string GetWorkRoot();

    string GetAgencyOutputDirectory(string agencia);

    string GetAgencyErrorDirectory(DateOnly date, string agencia);

    string GetDateErrorDirectory(DateOnly date);

    string GetCsvFilePath(DateOnly date);

    bool TryResolveAgencyFromEntradaPath(string pdfPath, out string agencia);

    bool TryResolveAgencyFromRawPath(string pdfPath, out string agencia);

    bool TryResolveAgencyFromStagingPath(string pdfPath, out string agencia);

    string GetProcesandoPath(string agencia, string fileName);

    string GetListoPath(string agencia, string fileName);

    [Obsolete("Use GetListoPath.")]
    string GetPreProcesadoPath(string agencia, string fileName);

    bool IsListoDeliveryPath(string pdfPath);

    bool IsIntakeEntradaPath(string pdfPath);

    bool TryLocateRelocatedPdf(string fileName, int maxAgeMinutes, out string locatedPath);
}
