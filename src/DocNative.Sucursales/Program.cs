using DocNative.Core.Configuration;
using DocNative.Core.Extensions;
using DocNative.Sucursales.Jobs;
using DocNative.Sucursales.Services;
using DocNative.Sucursales.Watching;
using DocNative.Sucursales.Workers;
using Microsoft.Extensions.Hosting.WindowsServices;
using Quartz;
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
builder.Services.AddSingleton<FileStabilityChecker>();
builder.Services.AddSingleton<SucursalResolver>();
builder.Services.AddSingleton<HotfolderWatcher>();
builder.Services.AddSingleton<DocumentProcessorService>();
builder.Services.AddHostedService<DocumentProcessingWorker>();

var docNativeOptions = builder.Configuration.GetSection(DocNativeOptions.SectionName).Get<DocNativeOptions>() ?? new DocNativeOptions();

builder.Services.AddQuartz(quartz =>
{
    CsvReportJobRegistration.RegisterCsvReportJob(quartz, docNativeOptions);
});

builder.Services.AddQuartzHostedService(options =>
{
    options.WaitForJobsToComplete = true;
});

var host = builder.Build();

Log.Information(
    "DocNative.Sucursales iniciando. RAW={RawRoot}, ENTRADA={OutputRoot}, ERROR={ErrorRoot}",
    docNativeOptions.RawRoot,
    docNativeOptions.OutputRoot,
    docNativeOptions.ErrorRoot);

await host.RunAsync();
