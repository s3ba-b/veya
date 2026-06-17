using Microsoft.Extensions.Options;
using Veya.Daemon;
using Veya.Daemon.Mcp;
using Veya.Daemon.Voice;
using Veya.Shared.Context;
using Veya.Shared.Inference;
using Veya.Shared.Notifications;
using Veya.Shared.Permissions;
using Veya.Shared.Safety;
using Veya.Shared.Voice;

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
    var ollamaOptions = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;

    // Audit logging is layered on by AuditingInferenceBackend (issue #89), the
    // single source of truth for the inference audit path: local.request for the
    // on-machine Ollama backend, cloud.request for the config-selected cloud one.
    IInferenceBackend local = new AuditingInferenceBackend(
        new OllamaBackend(new HttpClient(), ollamaOptions), auditLog, "ollama", ollamaOptions.Model, isLocal: true);

    // API keys resolve from IConfiguration first (dotnet user-secrets in dev,
    // ADR-0008), falling back to the documented flat env var for the service.
    var config = sp.GetRequiredService<IConfiguration>();
    IApiKeyProvider KeyProvider(string configKey, string envVar) =>
        new ConfigurationApiKeyProvider(config, configKey, new EnvironmentApiKeyProvider(envVar));

    // Cloud backend is config-selectable (ADR-0008): Mistral by default, Claude opt-in.
    var mistralOptions = sp.GetRequiredService<IOptions<MistralOptions>>().Value;
    (IInferenceBackend Backend, string Name, string Model) cloud = inference.CloudBackend.ToLowerInvariant() switch
    {
        "claude" => (new ClaudeBackend(KeyProvider("Anthropic:ApiKey", "ANTHROPIC_API_KEY"), inference.ClaudeModel), "claude", inference.ClaudeModel),
        "mistral" => (new MistralBackend(new HttpClient(), KeyProvider("Mistral:ApiKey", "MISTRAL_API_KEY"), mistralOptions), "mistral", mistralOptions.Model),
        var other => throw new InvalidOperationException($"Unknown Inference:CloudBackend '{other}'. Expected 'mistral' or 'claude'."),
    };

    IInferenceBackend auditedCloud = new AuditingInferenceBackend(cloud.Backend, auditLog, cloud.Name, cloud.Model, isLocal: false);

    return new FallbackInferenceBackend(local, auditedCloud);
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

// File context source (ADR-0010): indexes user-approved text folders. Inert until
// Context:Files:Roots is set and Permissions:Files is granted.
builder.Services.Configure<FileContextOptions>(builder.Configuration.GetSection("Context:Files"));
builder.Services.AddSingleton<IContextSource>(sp => new FileContextSource(sp.GetRequiredService<IOptions<FileContextOptions>>().Value));
builder.Services.AddSingleton(sp => new ContextIndexer(
    sp.GetRequiredService<IEmbeddingBackend>(),
    sp.GetRequiredService<IContextStore>(),
    sp.GetRequiredService<IPermissionGate>(),
    sp.GetRequiredService<IAuditLog>()));

// Notification intelligence (ADR-0011/0012): recent-store + digest, fed by a
// session-bus monitor of org.freedesktop.Notifications.Notify, all behind the
// Notifications permission. Inert without a session bus or without
// Permissions:Notifications granted (both degrade to no-ops).
builder.Services.AddSingleton<INotificationStore>(new InMemoryNotificationStore());
builder.Services.AddSingleton<INotificationSource, SessionBusNotificationSource>();
builder.Services.AddSingleton(sp => new NotificationDigestService(
    sp.GetRequiredService<INotificationStore>(),
    sp.GetRequiredService<IPermissionGate>(),
    sp.GetRequiredService<IAuditLog>()));

// Ask folds in every registered IContextProvider (ADR-0009 personal index,
// ADR-0011 notification digest, …), each degrading to "no context"
// independently. New sources register one more IContextProvider line here; the
// composite is assembled from the lot at the router below (issue #89, OCP).
builder.Services.AddSingleton<IContextProvider>(sp =>
    new ContextRetrievalProvider(sp.GetRequiredService<ContextRetriever>()));
builder.Services.AddSingleton<IContextProvider>(sp =>
    new NotificationDigestContextProvider(sp.GetRequiredService<NotificationDigestService>()));

// Voice I/O (ADR-0015): local Whisper.net STT + espeak-ng TTS. Both run
// through their own ISafeExecutor instance, separate from McpServer's — its
// allowlist only needs arecord/espeak-ng, and its timeout must cover
// Voice:MaxRecordingMs, well past McpServer's 5s default. Inert until
// Permissions:Microphone is granted and the Whisper model has been fetched
// (scripts/download-whisper-model.sh).
builder.Services.Configure<VoiceOptions>(builder.Configuration.GetSection("Voice"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<VoiceOptions>>().Value);
builder.Services.AddSingleton<ISafeExecutor>(sp =>
{
    var voiceOptions = sp.GetRequiredService<VoiceOptions>();
    var allowlist = ToolAllowlist.Combine(AlsaAudioRecorder.Allowlist, EspeakTextToSpeech.Allowlist);
    var timeout = TimeSpan.FromMilliseconds(voiceOptions.MaxRecordingMs) + TimeSpan.FromSeconds(5);
    return new SafeExecutor(allowlist, sp.GetRequiredService<IAuditLog>(), timeout);
});
builder.Services.AddSingleton<IAudioRecorder, AlsaAudioRecorder>();
builder.Services.AddSingleton<ISpeechToText, WhisperNetTranscriber>();
builder.Services.AddSingleton<ITextToSpeech, EspeakTextToSpeech>();
builder.Services.AddSingleton<IVoiceAskService, VoiceAskService>();

// The composite is built inline (not registered as IContextProvider), so
// GetServices returns only the leaf providers — no self-recursion.
builder.Services.AddSingleton<IModelRouter>(sp => new ModelRouter(
    sp.GetRequiredService<IInferenceBackend>(),
    sp.GetRequiredService<IMcpToolGateway>(),
    new CompositeContextProvider(sp.GetServices<IContextProvider>().ToList())));
builder.Services.AddSingleton<Veya1Service>();
builder.Services.AddHostedService<DBusHostedService>();
builder.Services.AddHostedService<ContextIndexingService>();
builder.Services.AddHostedService<NotificationCaptureService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
