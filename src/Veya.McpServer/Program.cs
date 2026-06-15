using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Veya.McpServer.Tools;
using Veya.Shared.Permissions;
using Veya.Shared.Safety;

var builder = Host.CreateApplicationBuilder(args);

// The stdio transport uses stdout for MCP protocol messages, so all logging
// must go to stderr.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton<IAuditLog>(_ => new JsonLinesAuditLog(AuditPaths.DefaultDirectory()));

// Per-source permissions (ADR-0005): default-deny, bound from the "Permissions"
// config section (e.g. Permissions:Clipboard=true). Any source not set is denied.
var permissionGrants = Enum.GetValues<PermissionSource>()
    .ToDictionary(source => source, source => builder.Configuration.GetValue($"Permissions:{source}", false));
builder.Services.AddSingleton<IPermissionStore>(new PermissionStore(permissionGrants));
builder.Services.AddSingleton<IPermissionGate, PermissionGate>();

builder.Services.AddSingleton(ToolAllowlist.Combine(SystemInfoTool.Allowlist, ProcessesTool.Allowlist, MemoryDiskTool.Allowlist, JournalTool.Allowlist, PackageTool.Allowlist, ServiceStatusTool.Allowlist, ClipboardTool.Allowlist, ScreenTool.Allowlist));
builder.Services.AddSingleton<ISafeExecutor, SafeExecutor>();
builder.Services.AddSingleton<IScreenCapture, PortalScreenshotClient>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<SystemInfoTool>()
    .WithTools<ProcessesTool>()
    .WithTools<MemoryDiskTool>()
    .WithTools<JournalTool>()
    .WithTools<PackageTool>()
    .WithTools<ServiceStatusTool>()
    .WithTools<ClipboardTool>()
    .WithTools<ScreenTool>();

var host = builder.Build();
await host.RunAsync();
