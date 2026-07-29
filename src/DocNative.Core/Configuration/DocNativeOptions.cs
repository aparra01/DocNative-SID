namespace DocNative.Core.Configuration;

public sealed class DocNativeOptions
{
    public const string SectionName = "DocNative";

    public const string ListoSubfolderName = "LISTO";

    /// <summary>Obsoleto: el escaneo va directo a <see cref="OutputRoot"/> (ENTRADA).</summary>
    [Obsolete("RawRoot ya no se usa; escaneo directo a OutputRoot (ENTRADA).")]
    public string RawRoot { get; set; } = @"C:\mnt\PagareOcrRaw";

    /// <summary>Carpeta vigilada: ENTRADA/&lt;codigo&gt;/ (escaneo MFP).</summary>
    public string OutputRoot { get; set; } = @"C:\mnt\PagareOcrEntrada";

    /// <summary>Carpeta temporal interna para claim exclusivo durante Render+Rewrite.</summary>
    public string WorkRoot { get; set; } = string.Empty;

    /// <summary>Obsoleto: usar <see cref="WorkRoot"/>.</summary>
    [Obsolete("Use WorkRoot. Carpeta temporal interna en lugar de PROCESANDO_DOCNATIVE.")]
    public string ProcesandoRoot { get; set; } = @"C:\mnt\PagareOcrProcesando";

    /// <summary>Obsoleto: entrega en ENTRADA/&lt;codigo&gt;/LISTO/.</summary>
    [Obsolete("Delivery is now ENTRADA/<codigo>/LISTO/.")]
    public string PreProcesadoRoot { get; set; } = @"C:\mnt\PagareOcrPreProcesado";

    /// <summary>Raíz SALIDA de PyVision; si <see cref="ErrorRoot"/> está vacío, errores en {SalidaRoot}/ERROR.</summary>
    public string SalidaRoot { get; set; } = @"C:\mnt\PagareOcrSalida";

    /// <summary>Errores centralizados (p. ej. SALIDA/ERROR). Vacío → {SalidaRoot}/ERROR.</summary>
    public string ErrorRoot { get; set; } = string.Empty;

    public double BlankPageThreshold { get; set; } = 0.02;

    public int RenderDpi { get; set; } = 150;

    public string CsvReportTime { get; set; } = "23:50";

    public int FileStabilityMaxWaitSeconds { get; set; } = 20;

    public double FileStabilityPollSeconds { get; set; } = 1.5;

    public int FileStabilityChecks { get; set; } = 3;

    public int PollingIntervalMs { get; set; } = 2000;

    public string SinSucursalCode { get; set; } = "SIN_SUCURSAL";
}
