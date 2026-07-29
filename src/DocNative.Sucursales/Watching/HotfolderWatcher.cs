using System.Collections.Concurrent;
using DocNative.Core.Abstractions;
using DocNative.Core.Configuration;
using Microsoft.Extensions.Options;

namespace DocNative.Sucursales.Watching;

public sealed class HotfolderWatcher : IDisposable
{
    private readonly DocNativeOptions _options;
    private readonly IPathLayout _pathLayout;
    private readonly ILogger<HotfolderWatcher> _logger;
    private readonly ConcurrentDictionary<string, byte> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lifecycleLock = new();
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;
    private bool _started;
    private bool _stopped;

    public event Func<string, Task>? PdfDetected;

    public HotfolderWatcher(
        IOptions<DocNativeOptions> options,
        IPathLayout pathLayout,
        ILogger<HotfolderWatcher> logger)
    {
        _options = options.Value;
        _pathLayout = pathLayout;
        _logger = logger;
    }

    public void Start()
    {
        lock (_lifecycleLock)
        {
            if (_started)
            {
                return;
            }

            _started = true;
        }

        Directory.CreateDirectory(_options.OutputRoot);
        Directory.CreateDirectory(_pathLayout.GetWorkRoot());

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

        _logger.LogInformation(
            "Hotfolder activo en ENTRADA {OutputRoot} (backlog WORK en {WorkRoot})",
            _options.OutputRoot,
            _pathLayout.GetWorkRoot());
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

        if (_pathLayout.IsListoDeliveryPath(path))
        {
            return;
        }

        if (!_pathLayout.IsIntakeEntradaPath(path) && !_pathLayout.Normalize(path).StartsWith(_pathLayout.GetWorkRoot(), StringComparison.OrdinalIgnoreCase))
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
                PollEntradaIntake(_options.OutputRoot);
                PollRoot(_pathLayout.GetWorkRoot());
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error en polling de hotfolder");
            }

            await Task.Delay(_options.PollingIntervalMs, cancellationToken).ConfigureAwait(false);
        }
    }

    private void PollEntradaIntake(string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var agencyDir in Directory.EnumerateDirectories(root))
        {
            if (string.Equals(
                    Path.GetFileName(agencyDir),
                    DocNativeOptions.ListoSubfolderName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(agencyDir, "*.pdf", SearchOption.TopDirectoryOnly))
            {
                EnqueueIfPdf(file);
            }
        }

        foreach (var file in Directory.EnumerateFiles(root, "*.pdf", SearchOption.TopDirectoryOnly))
        {
            EnqueueIfPdf(file);
        }
    }

    private void PollRoot(string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(root, "*.pdf", SearchOption.AllDirectories))
        {
            EnqueueIfPdf(file);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_lifecycleLock)
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
        }

        var pollCts = _pollCts;
        var pollTask = _pollTask;
        var watcher = _watcher;

        if (pollCts is not null)
        {
            try
            {
                await pollCts.CancelAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // otro hilo ya liberó el CTS durante el apagado del host
            }
        }

        if (pollTask is not null)
        {
            try
            {
                await pollTask.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                _logger.LogDebug("Polling de hotfolder no finalizó dentro del tiempo de espera");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // apagado forzado del host
            }
        }

        if (watcher is not null)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Created -= OnChanged;
            watcher.Changed -= OnChanged;
            watcher.Renamed -= OnRenamed;
            watcher.Dispose();
            _watcher = null;
        }

        pollCts?.Dispose();
        _pollCts = null;
        _pollTask = null;

        _logger.LogDebug("Hotfolder detenido");
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }
}
