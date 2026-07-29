namespace DocNative.Core.Configuration;

public sealed class DocNativeOptions
{
    public const string SectionName = "DocNative";

    public string RawRoot { get; set; } = @"C:\mnt\PagareOcrRaw";

    public string OutputRoot { get; set; } = @"C:\mnt\PagareOcrEntrada";

    public string ErrorRoot { get; set; } = @"C:\mnt\PagareOcrError";

    public double BlankPageThreshold { get; set; } = 0.02;

    public int RenderDpi { get; set; } = 150;

    public string CsvReportTime { get; set; } = "23:50";

    public int FileStabilityMaxWaitSeconds { get; set; } = 20;

    public double FileStabilityPollSeconds { get; set; } = 1.5;

    public int FileStabilityChecks { get; set; } = 3;

    public int PollingIntervalMs { get; set; } = 2000;

    public string SinSucursalCode { get; set; } = "SIN_SUCURSAL";
}
