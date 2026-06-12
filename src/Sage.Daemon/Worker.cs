namespace Sage.Daemon;

/// <summary>
/// Daemon skeleton. The D-Bus endpoint (org.sage.Sage1) and systemd
/// integration land in later issues; for now the service just runs until
/// asked to stop.
/// </summary>
public sealed class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Sage daemon skeleton started.");
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Sage daemon stopping.");
        }
    }
}
