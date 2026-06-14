namespace Veya.Daemon;

/// <summary>
/// Daemon skeleton. The D-Bus endpoint (org.veya.Veya1) and systemd
/// integration land in later issues; for now the service just runs until
/// asked to stop.
/// </summary>
public sealed class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Veya daemon skeleton started.");
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Veya daemon stopping.");
        }
    }
}
