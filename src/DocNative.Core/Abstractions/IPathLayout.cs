namespace DocNative.Core.Abstractions;

public interface IPathLayout
{
    string Normalize(string path);

    string GetAgencyOutputDirectory(string agencia);

    string GetAgencyErrorDirectory(DateOnly date, string agencia);

    string GetDateErrorDirectory(DateOnly date);

    string GetRegistryFilePath(DateOnly date);

    string GetCsvFilePath(DateOnly date);

    bool TryResolveAgencyFromRawPath(string pdfPath, out string agencia);
}
