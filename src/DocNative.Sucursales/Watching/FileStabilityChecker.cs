using System.Threading.Channels;
using DocNative.Core.Abstractions;
using DocNative.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocNative.Sucursales.Watching;

public sealed class FileStabilityChecker
{
    private readonly DocNativeOptions _options;
    private readonly ILogger<FileStabilityChecker> _logger;

    public FileStabilityChecker(IOptions<DocNativeOptions> options, ILogger<FileStabilityChecker> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> WaitUntilStableAsync(string filePath, CancellationToken cancellationToken)
    {
        var maxWait = TimeSpan.FromSeconds(_options.FileStabilityMaxWaitSeconds);
        var poll = TimeSpan.FromSeconds(_options.FileStabilityPollSeconds);
        var requiredChecks = Math.Max(1, _options.FileStabilityChecks);
        var started = DateTime.UtcNow;
        long lastSize = -1;
        var stableCount = 0;

        while (DateTime.UtcNow - started < maxWait)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var info = new FileInfo(filePath);
                if (!info.Exists || info.Length == 0)
                {
                    stableCount = 0;
                    lastSize = -1;
                }
                else if (info.Length == lastSize)
                {
                    stableCount++;
                    if (stableCount >= requiredChecks)
                    {
                        return true;
                    }
                }
                else
                {
                    lastSize = info.Length;
                    stableCount = 1;
                }
            }
            catch (IOException ex)
            {
                _logger.LogDebug(ex, "Esperando estabilidad de archivo {File}", filePath);
                stableCount = 0;
                lastSize = -1;
            }

            await Task.Delay(poll, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogWarning("Archivo no estabilizado a tiempo: {File}", filePath);
        return false;
    }
}
