using System.Net;
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

        var maxRetries = Math.Max(1, _options.PagareSplitValidationMaxRetries);
        var retryDelay = TimeSpan.FromSeconds(Math.Max(0, _options.PagareSplitValidationRetryDelaySeconds));
        var client = _httpClientFactory.CreateClient(nameof(SucursalesPdfOrderValidator));
        var requestUri = $"{_options.PagareSplitBaseUrl.TrimEnd('/')}/validar-orden-sucursales";

        HttpStatusCode? lastStatusCode = null;
        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var content = BuildMultipartContent(pdfPath);
                using var response = await client.PostAsync(requestUri, content, cancellationToken).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
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

                lastStatusCode = response.StatusCode;
                lastException = null;
                _logger.LogWarning(
                    "PagareSplit respondió {StatusCode} al validar orden | Archivo={Archivo} | Intento={Intento}/{MaxIntentos} | Body={Body}",
                    (int)response.StatusCode,
                    pdfPath,
                    attempt,
                    maxRetries,
                    body);

                if (!IsTransientStatusCode(response.StatusCode) || attempt >= maxRetries)
                {
                    break;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                lastException = ex;
                lastStatusCode = null;
                _logger.LogWarning(
                    ex,
                    "No se pudo validar orden con PagareSplit | Archivo={Archivo} | Intento={Intento}/{MaxIntentos}",
                    pdfPath,
                    attempt,
                    maxRetries);

                if (attempt >= maxRetries)
                {
                    break;
                }
            }

            if (attempt < maxRetries)
            {
                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        return (false, BuildFailureMessage(lastStatusCode, lastException));
    }

    private static MultipartFormDataContent BuildMultipartContent(string pdfPath)
    {
        var content = new MultipartFormDataContent();
        var stream = File.OpenRead(pdfPath);
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", Path.GetFileName(pdfPath));
        content.Add(new StringContent("160"), "dpi");
        return content;
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static string BuildFailureMessage(HttpStatusCode? statusCode, Exception? exception)
    {
        if (statusCode == HttpStatusCode.InternalServerError)
        {
            return "PagareSplit error interno (reintentos agotados)";
        }

        if (statusCode is HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout)
        {
            return "PagareSplit no disponible (reintentos agotados)";
        }

        if (exception is not null)
        {
            return "PagareSplit no disponible (reintentos agotados)";
        }

        return "PagareSplit no disponible (reintentos agotados)";
    }

    private sealed class PagareSplitValidationResponse
    {
        public bool Intercalado { get; set; }

        public string? Mensaje { get; set; }
    }
}
