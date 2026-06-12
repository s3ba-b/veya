using Microsoft.Extensions.Options;
using Sage.Daemon;
using Sage.Daemon.Mcp;
using Sage.Shared.Inference;
using Sage.Shared.Safety;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSystemd();
builder.Services.AddSingleton<IDBusSessionConnector, DBusSessionConnector>();
builder.Services.AddSingleton<IAuditLog>(_ => new JsonLinesAuditLog(AuditPaths.DefaultDirectory()));
builder.Services.AddSingleton<IApiKeyProvider, EnvironmentApiKeyProvider>();
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ollama"));
builder.Services.AddSingleton<IInferenceBackend>(sp =>
{
    var auditLog = sp.GetRequiredService<IAuditLog>();
    var local = new OllamaBackend(new HttpClient(), auditLog, sp.GetRequiredService<IOptions<OllamaOptions>>().Value);
    var cloud = new ClaudeBackend(sp.GetRequiredService<IApiKeyProvider>(), auditLog, "claude-sonnet-4-6");
    return new FallbackInferenceBackend(local, cloud);
});
builder.Services.Configure<McpServerOptions>(builder.Configuration.GetSection("Mcp"));
builder.Services.AddSingleton<IMcpToolGateway, McpToolGateway>();
builder.Services.AddSingleton<IModelRouter, ModelRouter>();
builder.Services.AddSingleton<Sage1Service>();
builder.Services.AddHostedService<DBusHostedService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
