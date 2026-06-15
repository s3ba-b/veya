using Veya.Shared.Context;

namespace Veya.Daemon;

/// <summary>
/// Indexes the registered <see cref="IContextSource"/>s into the personal
/// context index on daemon startup (ADR-0010), replacing each source's existing
/// chunks so the index stays consistent with disk. Runs off the critical path:
/// failures are logged, never fatal, and a denied permission simply indexes
/// nothing for that source.
/// </summary>
public sealed class ContextIndexingService(
    IEnumerable<IContextSource> sources,
    ContextIndexer indexer,
    ILogger<ContextIndexingService> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => RunAsync(stoppingToken);

    /// <summary>Indexes every registered source once. Exposed for tests.</summary>
    public async Task RunAsync(CancellationToken stoppingToken)
    {
        foreach (var source in sources)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                var result = await indexer.IngestAsync(source, replaceExisting: true, stoppingToken).ConfigureAwait(false);
                if (!result.PermissionGranted)
                {
                    logger.LogInformation("Context source {Source} not indexed: permission denied.", source.Source);
                }
                else if (result.EmbeddingUnavailable)
                {
                    logger.LogWarning(
                        "Context source {Source} partially indexed ({Count} chunks): embedding backend unavailable, will retry next start.",
                        source.Source, result.IndexedCount);
                }
                else
                {
                    logger.LogInformation("Context source {Source} indexed: {Count} chunks.", source.Source, result.IndexedCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Indexing context source {Source} failed.", source.Source);
            }
        }
    }
}
