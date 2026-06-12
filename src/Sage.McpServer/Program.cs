using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sage.McpServer.Tools;
using Sage.Shared.Safety;

var builder = Host.CreateApplicationBuilder(args);

// The stdio transport uses stdout for MCP protocol messages, so all logging
// must go to stderr.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton<IAuditLog>(_ => new JsonLinesAuditLog(AuditPaths.DefaultDirectory()));
builder.Services.AddSingleton(ToolAllowlist.Combine(SystemInfoTool.Allowlist, ProcessesTool.Allowlist, MemoryDiskTool.Allowlist, JournalTool.Allowlist, PackageTool.Allowlist, ServiceStatusTool.Allowlist));
builder.Services.AddSingleton<ISafeExecutor, SafeExecutor>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<SystemInfoTool>()
    .WithTools<ProcessesTool>()
    .WithTools<MemoryDiskTool>()
    .WithTools<JournalTool>()
    .WithTools<PackageTool>()
    .WithTools<ServiceStatusTool>();

var host = builder.Build();
await host.RunAsync();
