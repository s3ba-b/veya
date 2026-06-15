using Microsoft.Extensions.Options;
using Veya.Daemon;
using Veya.Daemon.Mcp;
using Veya.Shared.Inference;
using Veya.Shared.Safety;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSystemd();
builder.Services.AddSingleton<IDBusSessionConnector, DBusSessionConnector>();
// Single audit log, surfaced two ways: the append-only trail (IAuditLog) and a
// live activity feed for the D-Bus CloudUsage signal (IBackendActivityMonitor, issue #51).
builder.Services.AddSingleton(_ => new BackendActivityAuditLog(new JsonLinesAuditLog(AuditPaths.DefaultDirectory())));
builder.Services.AddSingleton<IAuditLog>(sp => sp.GetRequiredService<BackendActivityAuditLog>());
builder.Services.AddSingleton<IBackendActivityMonitor>(sp => sp.GetRequiredService<BackendActivityAuditLog>());
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ollama"));
builder.Services.Configure<MistralOptions>(builder.Configuration.GetSection("Mistral"));
builder.Services.Configure<InferenceOptions>(builder.Configuration.GetSection("Inference"));
builder.Services.AddSingleton<IInferenceBackend>(sp =>
{
    var auditLog = sp.GetRequiredService<IAuditLog>();
    var inference = sp.GetRequiredService<IOptions<InferenceOptions>>().Value;
    var local = new OllamaBackend(new HttpClient(), auditLog, sp.GetRequiredService<IOptions<OllamaOptions>>().Value);

    // API keys resolve from IConfiguration first (dotnet user-secrets in dev,
    // ADR-0008), falling back to the documented flat env var for the service.
    var config = sp.GetRequiredService<IConfiguration>();
    IApiKeyProvider KeyProvider(string configKey, string envVar) =>
        new ConfigurationApiKeyProvider(config, configKey, new EnvironmentApiKeyProvider(envVar));

    // Cloud backend is config-selectable (ADR-0008): Mistral by default, Claude opt-in.
    IInferenceBackend cloud = inference.CloudBackend.ToLowerInvariant() switch
    {
        "claude" => new ClaudeBackend(KeyProvider("Anthropic:ApiKey", "ANTHROPIC_API_KEY"), auditLog, inference.ClaudeModel),
        "mistral" => new MistralBackend(new HttpClient(), KeyProvider("Mistral:ApiKey", "MISTRAL_API_KEY"), auditLog, sp.GetRequiredService<IOptions<MistralOptions>>().Value),
        var other => throw new InvalidOperationException($"Unknown Inference:CloudBackend '{other}'. Expected 'mistral' or 'claude'."),
    };

    return new FallbackInferenceBackend(local, cloud);
});
builder.Services.Configure<McpServerOptions>(builder.Configuration.GetSection("Mcp"));
builder.Services.AddSingleton<IMcpToolGateway, McpToolGateway>();
builder.Services.AddSingleton<IModelRouter, ModelRouter>();
builder.Services.AddSingleton<Veya1Service>();
builder.Services.AddHostedService<DBusHostedService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
