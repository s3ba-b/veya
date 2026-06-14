namespace Veya.McpServer.Tools;

public sealed record DiskUsage(
    string Source,
    string MountPoint,
    string FilesystemType,
    long SizeBytes,
    long UsedBytes,
    long AvailableBytes,
    int UsePercent);
