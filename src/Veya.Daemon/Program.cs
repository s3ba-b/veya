using Microsoft.Extensions.Options;
using Veya.Daemon;
using Veya.Daemon.Mcp;
using Veya.Shared.Context;
using Veya.Shared.Inference;
using Veya.Shared.Notifications;
using Veya.Shared.Permissions;
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

// Personal context index (ADR-0009): local-first embeddings + sqlite-vec store,
// behind the same per-source permission gate as every other context source
// (ADR-0005), bound default-deny from the "Permissions" config section.
var contextGrants = Enum.GetValues<PermissionSource>()
    .ToDictionary(source => source, source => builder.Configuration.GetValue($"Permissions:{source}", false));
builder.Services.AddSingleton<IPermissionStore>(new PermissionStore(contextGrants));
builder.Services.AddSingleton<IPermissionGate, PermissionGate>();
builder.Services.AddSingleton<IEmbeddingBackend>(sp =>
    new OllamaEmbeddingBackend(new HttpClient(), sp.GetRequiredService<IAuditLog>(), sp.GetRequiredService<IOptions<OllamaOptions>>().Value));
builder.Services.AddSingleton<IContextStore>(_ =>
{
    var dbPath = ContextPaths.DefaultDatabasePath();
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
    return new SqliteContextStore($"Data Source={dbPath}");
});
builder.Services.AddSingleton(sp => new ContextRetriever(
    sp.GetRequiredService<IEmbeddingBackend>(),
    sp.GetRequiredService<IContextStore>(),
    sp.GetRequiredService<IPermissionGate>(),
    sp.GetRequiredService<IAuditLog>(),
    candidateSources: [PermissionSource.PersonalIndex, PermissionSource.Files]));
builder.Services.AddSingleton<IContextProvider>(sp => new ContextRetrievalProvider(sp.GetRequiredService<ContextRetriever>()));

// File context source (ADR-0010): indexes user-approved text folders. Inert until
// Context:Files:Roots is set and Permissions:Files is granted.
builder.Services.Configure<FileContextOptions>(builder.Configuration.GetSection("Context:Files"));
builder.Services.AddSingleton<IContextSource>(sp => new FileContextSource(sp.GetRequiredService<IOptions<FileContextOptions>>().Value));
builder.Services.AddSingleton(sp => new ContextIndexer(
    sp.GetRequiredService<IEmbeddingBackend>(),
    sp.GetRequiredService<IContextStore>(),
    sp.GetRequiredService<IPermissionGate>(),
    sp.GetRequiredService<IAuditLog>()));

// Notification intelligence (ADR-0011): recent-store + digest, behind the
// Notifications permission. Inert until the real session-bus source lands and
// Permissions:Notifications is granted — no source/capture service registered yet.
builder.Services.AddSingleton<INotificationStore>(new InMemoryNotificationStore());
builder.Services.AddSingleton(sp => new NotificationDigestService(
    sp.GetRequiredService<INotificationStore>(),
    sp.GetRequiredService<IPermissionGate>(),
    sp.GetRequiredService<IAuditLog>()));

builder.Services.AddSingleton<IModelRouter, ModelRouter>();
builder.Services.AddSingleton<Veya1Service>();
builder.Services.AddHostedService<DBusHostedService>();
builder.Services.AddHostedService<ContextIndexingService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
