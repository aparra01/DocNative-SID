using DocNative.Core.Abstractions;
using DocNative.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace DocNative.Sucursales.Jobs;

public sealed class CsvReportJob : IJob
{
    private readonly IPathLayout _pathLayout;
    private readonly IErrorRecordStore _errorRecordStore;
    private readonly ICsvReportGenerator _csvReportGenerator;
    private readonly ILogger<CsvReportJob> _logger;

    public CsvReportJob(
        IPathLayout pathLayout,
        IErrorRecordStore errorRecordStore,
        ICsvReportGenerator csvReportGenerator,
        ILogger<CsvReportJob> logger)
    {
        _pathLayout = pathLayout;
        _errorRecordStore = errorRecordStore;
        _csvReportGenerator = csvReportGenerator;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var date = DateOnly.FromDateTime(DateTime.Now);
        await _errorRecordStore.LoadPersistedRecordsAsync(date, context.CancellationToken).ConfigureAwait(false);
        var records = await _errorRecordStore.GetRecordsForDateAsync(date, context.CancellationToken).ConfigureAwait(false);
        var outputDirectory = _pathLayout.GetDateErrorDirectory(date);

        var csvPath = await _csvReportGenerator.GenerateAsync(date, records, outputDirectory, context.CancellationToken).ConfigureAwait(false);
        _logger.LogInformation("CSV diario generado en {CsvPath} con {Count} registros", csvPath, records.Count);
    }
}

public static class CsvReportJobRegistration
{
    public static void RegisterCsvReportJob(IServiceCollectionQuartzConfigurator quartz, DocNativeOptions options)
    {
        if (!TimeOnly.TryParse(options.CsvReportTime, out var reportTime))
        {
            reportTime = new TimeOnly(23, 50);
        }

        var cron = $"0 {reportTime.Minute} {reportTime.Hour} * * ?";

        var jobKey = new JobKey("CsvReportJob");
        quartz.AddJob<CsvReportJob>(cfg => cfg.WithIdentity(jobKey));
        quartz.AddTrigger(cfg => cfg
            .ForJob(jobKey)
            .WithIdentity("CsvReportJob-trigger")
            .WithCronSchedule(cron));
    }
}
