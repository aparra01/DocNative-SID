using System.Net;
using System.Text;
using DocNative.Core.Configuration;
using DocNative.Sucursales.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DocNative.Core.Tests;

public class SucursalesPdfOrderValidatorTests
{
    [Fact]
    public async Task ValidateAsync_SucceedsOnThirdAttemptAfterTransient500()
    {
        var pdfPath = CreateTempPdf();
        var handler = new QueueHttpMessageHandler(
            HttpResponse(500, "Internal Server Error"),
            HttpResponse(500, "Internal Server Error"),
            HttpResponse(200, """{"intercalado":false}"""));
        var validator = CreateValidator(handler, maxRetries: 3, retryDelaySeconds: 0);

        var (isValid, errorMessage) = await validator.ValidateAsync(pdfPath);

        Assert.True(isValid);
        Assert.Null(errorMessage);
        Assert.Equal(3, handler.RequestCount);
        CleanupFile(pdfPath);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsInternalErrorAfterPersistent500()
    {
        var pdfPath = CreateTempPdf();
        var handler = new QueueHttpMessageHandler(
            HttpResponse(500, "Internal Server Error"),
            HttpResponse(500, "Internal Server Error"),
            HttpResponse(500, "Internal Server Error"));
        var validator = CreateValidator(handler, maxRetries: 3, retryDelaySeconds: 0);

        var (isValid, errorMessage) = await validator.ValidateAsync(pdfPath);

        Assert.False(isValid);
        Assert.Equal("PagareSplit error interno (reintentos agotados)", errorMessage);
        Assert.Equal(3, handler.RequestCount);
        CleanupFile(pdfPath);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsUnavailableAfterPersistent503()
    {
        var pdfPath = CreateTempPdf();
        var handler = new QueueHttpMessageHandler(HttpResponse(503, "Service Unavailable"));
        var validator = CreateValidator(handler, maxRetries: 1, retryDelaySeconds: 0);

        var (isValid, errorMessage) = await validator.ValidateAsync(pdfPath);

        Assert.False(isValid);
        Assert.Equal("PagareSplit no disponible (reintentos agotados)", errorMessage);
        CleanupFile(pdfPath);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsInterleavedMessageWithoutRetry()
    {
        var pdfPath = CreateTempPdf();
        var handler = new QueueHttpMessageHandler(
            HttpResponse(200, """{"intercalado":true,"mensaje":"PDF mal ordenado: operaciones intercaladas (OP1: pág. 1,3)."}"""));
        var validator = CreateValidator(handler, maxRetries: 3, retryDelaySeconds: 0);

        var (isValid, errorMessage) = await validator.ValidateAsync(pdfPath);

        Assert.False(isValid);
        Assert.Contains("PDF mal ordenado", errorMessage, StringComparison.Ordinal);
        Assert.Equal(1, handler.RequestCount);
        CleanupFile(pdfPath);
    }

    [Fact]
    public async Task ValidateAsync_SkipsValidationWhenDisabled()
    {
        var pdfPath = CreateTempPdf();
        var handler = new QueueHttpMessageHandler(HttpResponse(500, "fail"));
        var validator = CreateValidator(
            handler,
            maxRetries: 3,
            retryDelaySeconds: 0,
            enableValidation: false);

        var (isValid, errorMessage) = await validator.ValidateAsync(pdfPath);

        Assert.True(isValid);
        Assert.Null(errorMessage);
        Assert.Equal(0, handler.RequestCount);
        CleanupFile(pdfPath);
    }

    private static SucursalesPdfOrderValidator CreateValidator(
        QueueHttpMessageHandler handler,
        int maxRetries,
        int retryDelaySeconds,
        bool enableValidation = true)
    {
        var options = Options.Create(new DocNativeOptions
        {
            EnableInterleavedPdfValidation = enableValidation,
            PagareSplitBaseUrl = "http://pagaresplit.test",
            PagareSplitValidationMaxRetries = maxRetries,
            PagareSplitValidationRetryDelaySeconds = retryDelaySeconds,
        });

        var factory = new TestHttpClientFactory(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://pagaresplit.test"),
        });

        return new SucursalesPdfOrderValidator(
            options,
            factory,
            new PagareSplitValidationGate(options, NullLogger<PagareSplitValidationGate>.Instance),
            NullLogger<SucursalesPdfOrderValidator>.Instance);
    }

    private static HttpResponseMessage HttpResponse(int statusCode, string body) =>
        new((HttpStatusCode)statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private static string CreateTempPdf()
    {
        var path = Path.Combine(Path.GetTempPath(), $"docnative-validator-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, "%PDF-1.4 test"u8.ToArray());
        return path;
    }

    private static void CleanupFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed class TestHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class QueueHttpMessageHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No hay más respuestas en cola.");
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }
}
