using System.Net.Http.Headers;
using System.Text.Json;
using DocNative.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocNative.Sucursales.Services;

/// <summary>
/// Validación de orden de operaciones para el flujo sucursales vía PagareSplit-SID.
/// </summary>
public sealed class SucursalesPdfOrderValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly DocNativeOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SucursalesPdfOrderValidator> _logger;

    public SucursalesPdfOrderValidator(
        IOptions<DocNativeOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<SucursalesPdfOrderValidator> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<(bool IsValid, string? ErrorMessage)> ValidateAsync(
        string pdfPath,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableInterleavedPdfValidation)
        {
            return (true, null);
        }

        if (string.IsNullOrWhiteSpace(_options.PagareSplitBaseUrl))
        {
            _logger.LogWarning(
                "Validación de PDF intercalado omitida: PagareSplitBaseUrl no configurada | Archivo={Archivo}",
                pdfPath);
            return (true, null);
        }

        if (!File.Exists(pdfPath))
        {
            return (true, null);
        }

        try
        {
            using var content = new MultipartFormDataContent();
            await using var stream = File.OpenRead(pdfPath);
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Add(fileContent, "file", Path.GetFileName(pdfPath));
            content.Add(new StringContent("160"), "dpi");

            var client = _httpClientFactory.CreateClient(nameof(SucursalesPdfOrderValidator));
            var baseUrl = _options.PagareSplitBaseUrl.TrimEnd('/');
            var requestUri = $"{baseUrl}/validar-orden-sucursales";

            using var response = await client.PostAsync(requestUri, content, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "PagareSplit respondió {StatusCode} al validar orden | Archivo={Archivo} | Body={Body}",
                    (int)response.StatusCode,
                    pdfPath,
                    body);
                return (false, "No se pudo validar el orden del PDF (PagareSplit no disponible)");
            }

            var parsed = JsonSerializer.Deserialize<PagareSplitValidationResponse>(body, JsonOptions);
            if (parsed?.Intercalado == true)
            {
                var message = string.IsNullOrWhiteSpace(parsed.Mensaje)
                    ? "PDF mal ordenado"
                    : parsed.Mensaje;
                _logger.LogWarning(
                    "PDF rechazado por operaciones intercaladas | Archivo={Archivo} | Detalle={Detalle}",
                    pdfPath,
                    message);
                return (false, message);
            }

            return (true, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            _logger.LogWarning(
                ex,
                "No se pudo validar orden con PagareSplit | Archivo={Archivo}",
                pdfPath);
            return (false, "No se pudo validar el orden del PDF (PagareSplit no disponible)");
        }
    }

    private sealed class PagareSplitValidationResponse
    {
        public bool Intercalado { get; set; }

        public string? Mensaje { get; set; }
    }
}
