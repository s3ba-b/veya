namespace Veya.McpServer.Tools;

public sealed record ProcessInfo(
    int Pid,
    double CpuPercent,
    double MemoryPercent,
    long ResidentSetSizeBytes,
    string Command);
