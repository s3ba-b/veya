namespace Veya.McpServer.Tools;

public sealed record SystemInfo(
    string Hostname,
    string OperatingSystem,
    string KernelVersion,
    string Architecture,
    TimeSpan Uptime);
