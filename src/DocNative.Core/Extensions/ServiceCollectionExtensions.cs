using DocNative.Core.Abstractions;
using DocNative.Core.Configuration;
using DocNative.Core.Errors;
using DocNative.Core.Imaging;
using DocNative.Core.Pdf;
using DocNative.Core.Pipeline;
using DocNative.Core.Paths;
using Microsoft.Extensions.DependencyInjection;

namespace DocNative.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDocNativeCore(this IServiceCollection services)
    {
        services.AddSingleton<IBlankPageDetector, BlankPageDetector>();
        services.AddSingleton<IRotationCorrector, RotationCorrector>();
        services.AddSingleton<IPdfRenderer, PdfRenderService>();
        services.AddSingleton<IPdfRewriter, PdfRewriteService>();
        services.AddSingleton<IDocumentPipeline, DocumentPipeline>();
        services.AddSingleton<IPathLayout, PathLayout>();
        services.AddSingleton<IErrorRecordStore, ErrorRecordStore>();
        services.AddSingleton<IErrorHandler, ErrorHandler>();
        return services;
    }
}
