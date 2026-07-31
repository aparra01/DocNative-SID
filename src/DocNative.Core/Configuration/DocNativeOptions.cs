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

    /// <summary>Raíz SALIDA de PyVision; si <see cref="ErrorRoot"/> está vacío, errores según layout.</summary>
    public string SalidaRoot { get; set; } = @"C:\mnt\PagareOcrSalida";

    /// <summary>Errores centralizados. Vacío → {SalidaRoot}/ERROR o {SalidaRoot}/00 - ERROR según layout.</summary>
    public string ErrorRoot { get; set; } = string.Empty;

    /// <summary>Carpeta de error central PagareOCR sucursales (compatible con PyVision).</summary>
    public const string CentralErrorDirName = "00 - ERROR";

    /// <summary>
    /// Flujo sucursales: errores en {SalidaRoot}/00 - ERROR/YYYYMMDD/ y CSV {YYYYMMDD}_error.csv en la raíz.
    /// </summary>
    public bool UsePagareOcrCentralErrorLayout { get; set; }

    public double BlankPageThreshold { get; set; } = 0.02;

    /// <summary>Fracción máxima de tinta (Otsu) para considerar una página en blanco.</summary>
    public double BlankPageInkRatioThreshold { get; set; } = 0.015;

    public int RenderDpi { get; set; } = 150;

    public string TesseractExecutablePath { get; set; } = @"C:\Program Files\Tesseract-OCR\tesseract.exe";

    public double OsdMinConfidence { get; set; } = 1.0;

    /// <summary>Umbral OSD más bajo cuando la rotación detectada no es 0° (p. ej. páginas volteadas).</summary>
    public double OsdMinConfidenceForRotation { get; set; } = 0.3;

    /// <summary>Confianza mínima para aceptar OSD=0° sin verificación adicional (flip-check).</summary>
    public double OsdMinConfidenceForUpright { get; set; } = 2.0;

    /// <summary>Mínimo de caracteres que Tesseract intenta para OSD (default nativo: 50).</summary>
    public int OsdMinCharactersToTry { get; set; } = 10;

    /// <summary>Tamaño máximo del lado largo de la imagen enviada a OSD.</summary>
    public int OsdMaxEdgePixels { get; set; } = 2000;

    /// <summary>Aplica la rotación mayoritaria del documento a páginas ambiguas (p. ej. hojas de continuación).</summary>
    public bool EnableDocumentRotationConsensus { get; set; } = true;

    /// <summary>Fracción mínima de páginas con contenido que deben coincidir para imponer consenso.</summary>
    public double DocumentRotationConsensusMinShare { get; set; } = 0.6;

    public bool EnableDeskew { get; set; } = true;

    public double MaxSkewDegrees { get; set; } = 15;

    public double MinSkewDegrees { get; set; } = 0.3;

    public int TesseractTimeoutSeconds { get; set; } = 30;

    public int FileStabilityMaxWaitSeconds { get; set; } = 20;

    public double FileStabilityPollSeconds { get; set; } = 1.5;

    public int FileStabilityChecks { get; set; } = 3;

    public int PollingIntervalMs { get; set; } = 2000;

    public string SinSucursalCode { get; set; } = "SIN_SUCURSAL";

    /// <summary>
    /// Flujo sucursales (PagareOCR): rechaza PDFs con operaciones intercaladas antes de LISTO/.
    /// Desactivado por defecto; activar solo en <c>DocNative.Sucursales</c>.
    /// </summary>
    public bool EnableInterleavedPdfValidation { get; set; }

    /// <summary>URL base de PagareSplit-SID para validar orden (ej. http://pagaresplit:8006).</summary>
    public string PagareSplitBaseUrl { get; set; } = string.Empty;

    /// <summary>Timeout en segundos para validación remota con PagareSplit.</summary>
    public int PagareSplitValidationTimeoutSeconds { get; set; } = 120;
}
