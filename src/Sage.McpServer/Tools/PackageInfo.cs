namespace Sage.McpServer.Tools;

public sealed record PackageInfo(
    string Name,
    bool Installed,
    string? Version,
    string? Architecture,
    string? Description);
