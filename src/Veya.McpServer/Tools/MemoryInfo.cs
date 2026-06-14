namespace Veya.McpServer.Tools;

public sealed record MemoryInfo(
    long TotalBytes,
    long UsedBytes,
    long FreeBytes,
    long SharedBytes,
    long BuffCacheBytes,
    long AvailableBytes,
    long SwapTotalBytes,
    long SwapUsedBytes,
    long SwapFreeBytes);
