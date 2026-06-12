using Microsoft.Extensions.Logging.Abstractions;
using Sage.Daemon;
using Xunit;

namespace Sage.Daemon.Tests;

public class WorkerTests
{
    [Fact]
    public async Task Worker_StartsAndStopsCleanly()
    {
        // Headless by design: no D-Bus, no display server (hard rule #3).
        using var worker = new Worker(NullLogger<Worker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        Assert.NotNull(worker.ExecuteTask);
        Assert.True(worker.ExecuteTask.IsCompleted);
    }
}
