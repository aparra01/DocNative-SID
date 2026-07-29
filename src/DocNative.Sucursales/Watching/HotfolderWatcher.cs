using System.Collections.Concurrent;
using DocNative.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocNative.Sucursales.Watching;

public sealed class HotfolderWatcher : IDisposable
{
    private readonly DocNativeOptions _options;
    private readonly ILogger<HotfolderWatcher> _logger;
    private readonly ConcurrentDictionary<string, byte> _pending = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;

    public event Func<string, Task>? PdfDetected;

    public HotfolderWatcher(IOptions<DocNativeOptions> options, ILogger<HotfolderWatcher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public void Start()
    {
        Directory.CreateDirectory(_options.OutputRoot);
        _watcher = new FileSystemWatcher(_options.OutputRoot)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            Filter = "*.*",
            EnableRaisingEvents = true
        };

        _watcher.Created += OnChanged;
        _watcher.Changed += OnChanged;
        _watcher.Renamed += OnRenamed;

        _pollCts = new CancellationTokenSource();
        _pollTask = Task.Run(() => PollExistingFilesAsync(_pollCts.Token));

        _logger.LogInformation("Hotfolder activo en ENTRADA {OutputRoot}", _options.OutputRoot);
    }

    private void OnChanged(object sender, FileSystemEventArgs e) => EnqueueIfPdf(e.FullPath);

    private void OnRenamed(object sender, RenamedEventArgs e) => EnqueueIfPdf(e.FullPath);

    private void EnqueueIfPdf(string path)
    {
        if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (path.EndsWith(".processing", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!_pending.TryAdd(path, 0))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500).ConfigureAwait(false);
                if (PdfDetected is not null)
                {
                    await PdfDetected.Invoke(path).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error despachando PDF detectado {Path}", path);
            }
            finally
            {
                _pending.TryRemove(path, out _);
            }
        });
    }

    private async Task PollExistingFilesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (Directory.Exists(_options.OutputRoot))
                {
                    foreach (var file in Directory.EnumerateFiles(_options.OutputRoot, "*.pdf", SearchOption.AllDirectories))
                    {
                        EnqueueIfPdf(file);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error en polling de hotfolder");
            }

            await Task.Delay(_options.PollingIntervalMs, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        _pollCts?.Cancel();
        if (_pollTask is not null)
        {
            try
            {
                _pollTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // ignore shutdown race
            }
        }

        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnChanged;
            _watcher.Changed -= OnChanged;
            _watcher.Renamed -= OnRenamed;
            _watcher.Dispose();
        }

        _pollCts?.Dispose();
    }
}
