namespace Sage.McpServer.Tools;

public sealed record ServiceStatus(
    string Unit,
    string LoadState,
    string ActiveState,
    string SubState,
    string UnitFileState);
