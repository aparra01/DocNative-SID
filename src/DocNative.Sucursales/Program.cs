using DocNative.Core.Configuration;
using DocNative.Core.Extensions;
using DocNative.Sucursales.Services;
using DocNative.Sucursales.Watching;
using DocNative.Sucursales.Workers;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(AppContext.BaseDirectory, "logs", "docnative-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14)
    .CreateLogger();

builder.Services.AddSerilog();
builder.Services.AddWindowsService(options => options.ServiceName = "DocNative.Sucursales");

builder.Services.Configure<DocNativeOptions>(builder.Configuration.GetSection(DocNativeOptions.SectionName));
builder.Services.AddDocNativeCore();
builder.Services.AddHttpClient(
    nameof(SucursalesPdfOrderValidator),
    (sp, client) =>
    {
        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DocNativeOptions>>().Value;
        client.Timeout = TimeSpan.FromSeconds(Math.Max(10, options.PagareSplitValidationTimeoutSeconds));
    });
builder.Services.AddSingleton<SucursalesPdfOrderValidator>();
builder.Services.AddSingleton<FileStabilityChecker>();
builder.Services.AddSingleton<SucursalResolver>();
builder.Services.AddSingleton<HotfolderWatcher>();
builder.Services.AddSingleton<DocumentProcessorService>();
builder.Services.AddHostedService<DocumentProcessingWorker>();

var docNativeOptions = builder.Configuration.GetSection(DocNativeOptions.SectionName).Get<DocNativeOptions>() ?? new DocNativeOptions();

var host = builder.Build();

var pathLayout = host.Services.GetRequiredService<DocNative.Core.Abstractions.IPathLayout>();
Log.Information(
    "DocNative.Sucursales iniciando. ENTRADA={OutputRoot}, WORK={WorkRoot}, LISTO={ListoSubfolder}, SALIDA={SalidaRoot}, ERROR={ErrorRoot}, ValidarIntercalado={ValidarIntercalado}, PagareSplit={PagareSplitUrl}",
    docNativeOptions.OutputRoot,
    pathLayout.GetWorkRoot(),
    DocNativeOptions.ListoSubfolderName,
    docNativeOptions.SalidaRoot,
    string.IsNullOrWhiteSpace(docNativeOptions.ErrorRoot)
        ? Path.Combine(docNativeOptions.SalidaRoot, "ERROR")
        : docNativeOptions.ErrorRoot,
    docNativeOptions.EnableInterleavedPdfValidation,
    string.IsNullOrWhiteSpace(docNativeOptions.PagareSplitBaseUrl)
        ? "(no configurado)"
        : docNativeOptions.PagareSplitBaseUrl);

await host.RunAsync();
